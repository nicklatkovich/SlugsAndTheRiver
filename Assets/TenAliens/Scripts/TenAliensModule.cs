﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TenAliensModule : MonoBehaviour {
	private const float HOLDING = .5f;
	private const float ALIEN_TRANSFER_LENGTH = .075f;
	private const string SOLVING_SOUND = "alien_solved";
	private readonly Vector3 ALIENS_OFFSET = new Vector3(.025f, .001f, .025f);
	private readonly string[] ALIEN_CLICK_SOUNDS = new[] { "alien_click_1", "alien_click_2", "alien_click_3" };
	private readonly string[] ALIEN_PULL_SOUNDS = new[] { "alien_click_1", "alien_click_2", "alien_click_3" };
	private readonly string[] ALIEN_SELF_TRANSFER_SOUNDS = new[] { "alien_self_transfer_1", "alien_self_transfer_2", "alien_self_transfer_3" };
	private readonly string[] ALIEN_STRIKE_SOUNDS = new[] { "alien_strike_1", "alien_strike_2" };

	public GameObject AliensContainer;
	public TextMesh Energy;
	public KMSelectable Selectable;
	public KMSelectable ResetButton;
	public KMBombModule Module;
	public KMAudio Audio;
	public AlienComponent AlienPrefab;

	private bool solved;
	private bool resetHolded;
	private float holdingTimer;
	private AlienComponent holdedAlien;
	private AlienComponent selectedAlien;
	private AlienComponent[] aliens;
	private TenAliensPuzzle puzzle;

	private void Start() {
		puzzle = new TenAliensPuzzle(10);
		List<KMSelectable> children = new List<KMSelectable>();
		List<AlienComponent> aliensList = new List<AlienComponent>();
		for (int i = 0; i < 10; i++) {
			AlienComponent alien = Instantiate(AlienPrefab);
			alien.data = puzzle.southAliens.First(s => s.id == i);
			aliensList.Add(alien);
			alien.transform.parent = AliensContainer.transform;
			SetAlienInitialPosition(alien);
			alien.transform.localScale = Vector3.one;
			alien.transform.localRotation = Quaternion.identity;
			alien.Selectable.OnInteract += () => { HoldAlien(alien); return false; };
			alien.Selectable.OnInteractEnded += () => UnholdAlien();
			alien.Selectable.Parent = Selectable;
			children.Add(alien.Selectable);
		}
		aliens = aliensList.ToArray();
		children.Add(ResetButton);
		Selectable.Children = children.ToArray();
		Selectable.UpdateChildren();
		Energy.text = puzzle.energy.ToString();
		ResetButton.OnInteract += () => { HoldReset(); return false; };
		ResetButton.OnInteractEnded += () => UnholdReset();
	}

	private void Update() {
		if (holdingTimer > 0) {
			if (holdingTimer < Time.time) {
				if (holdedAlien != null) UnholdAlien(true);
				else if (resetHolded) UnholdReset();
				else holdingTimer = 0;
			}
		}
	}

	private void HoldAlien(AlienComponent alien) {
		holdingTimer = Time.time + HOLDING;
		holdedAlien = alien;
		if (puzzle.southAliens.Any(s => s.id == holdedAlien.data.id)) PlayClick(holdedAlien.transform);
	}

	private void HoldReset() {
		Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
		holdingTimer = Time.time + HOLDING;
		resetHolded = true;
	}

	private void UnholdReset(bool forceHold = false) {
		if (!resetHolded) return;
		Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, transform);
		bool tap = forceHold ? false : holdingTimer > Time.time;
		holdingTimer = 0;
		if (tap) return;
		if (puzzle.northAliens.Count == 0) return;
		foreach (AlienComponent alien in aliens) SetAlienInitialPosition(alien);
		if (solved && puzzle.southAliens.Count == 0 && puzzle.energy >= 0) {
			puzzle = new TenAliensPuzzle(10);
			foreach (TenAliensPuzzle.Alien data in puzzle.southAliens) aliens[data.id].data = data;
		} else {
			puzzle.reset();
			Strike();
		}
		Energy.text = puzzle.energy.ToString();
	}

	private void UnholdAlien(bool forceHold = false) {
		if (holdedAlien == null) return;
		bool tap = forceHold ? false : holdingTimer > Time.time;
		holdingTimer = 0;
		if (tap) {
			if (selectedAlien == null) {
				if (!puzzle.northAliens.Any(s => s.id == holdedAlien.data.id)) return;
				selectedAlien = holdedAlien;
				selectedAlien.selected = true;
				holdedAlien = null;
				PlayClick(selectedAlien.transform);
				return;
			}
			if (selectedAlien == holdedAlien) {
				selectedAlien.selected = false;
				PlayClick(selectedAlien.transform);
				selectedAlien = null;
				holdedAlien = null;
				return;
			}
			if (puzzle.northAliens.Any(s => s.id == holdedAlien.data.id)) {
				selectedAlien.selected = false;
				selectedAlien = holdedAlien;
				selectedAlien.selected = true;
				PlayClick(selectedAlien.transform);
				holdedAlien = null;
				return;
			}
			if (holdedAlien.data.conflict(selectedAlien.data)) {
				selectedAlien.selected = false;
				selectedAlien = null;
				holdedAlien = null;
				Strike();
				return;
			}
			PlayPull(holdedAlien.transform);
			puzzle.pull(holdedAlien.data.id, selectedAlien.data.id);
			selectedAlien.selected = false;
			selectedAlien = null;
			holdedAlien.transform.localPosition += Vector3.forward * ALIEN_TRANSFER_LENGTH;
			holdedAlien = null;
			Energy.text = puzzle.energy.ToString();
			if (puzzle.southAliens.Count == 0 && puzzle.energy >= 0) Solve();
			return;
		}
		if (puzzle.southAliens.Any(s => s.id == holdedAlien.data.id)) {
			PlayStartSelfTransfer(holdedAlien.transform);
			puzzle.transfer(holdedAlien.data.id);
			holdedAlien.transform.localPosition += Vector3.forward * ALIEN_TRANSFER_LENGTH;
			holdedAlien = null;
			Energy.text = puzzle.energy.ToString();
			if (puzzle.southAliens.Count == 0 && puzzle.energy >= 0) Solve();
		}
	}

	private void SetAlienInitialPosition(AlienComponent alien) {
		int id = alien.data.id;
		alien.transform.localPosition = new Vector3((id % 5 - 2f) * ALIENS_OFFSET.x, ALIENS_OFFSET.y, (id / 5 - .5f) * ALIENS_OFFSET.z - .04f);
	}

	private void Strike() {
		Audio.PlaySoundAtTransform(ALIEN_STRIKE_SOUNDS.PickRandom(), transform);
		if (solved) return;
		Module.HandleStrike();
	}

	private void Solve() {
		Audio.PlaySoundAtTransform(SOLVING_SOUND, transform);
		if (solved) return;
		Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
		solved = true;
		Module.HandlePass();
	}

	private void PlayClick(Transform transform) {
		Audio.PlaySoundAtTransform(ALIEN_CLICK_SOUNDS.PickRandom(), transform);
	}

	private void PlayPull(Transform transform) {
		Audio.PlaySoundAtTransform(ALIEN_PULL_SOUNDS.PickRandom(), transform);
	}

	private void PlayStartSelfTransfer(Transform transform) {
		Audio.PlaySoundAtTransform(ALIEN_SELF_TRANSFER_SOUNDS.PickRandom(), transform);
	}
}
