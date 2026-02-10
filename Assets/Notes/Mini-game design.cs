
//# 🌌 Star Map & Exploration System – Design Summary

//## High-Level Goal

//The star map is one of the **two primary progression pillars** of the game (alongside boat upgrades).
//It represents **knowledge of the world**, not power, and must feel **earned through risk and navigation**, not menus or chores.

//---

//## Core Principles

//### 1. Knowledge, Not Unlocks

//Players do not “unlock routes” directly.
//They **learn that routes exist**, then **learn enough to trust them**.

//Internally, each route (edge) has numeric progress.
//Externally, players see **qualitative knowledge states**, never meters.

//---

//### 2. Per-Edge, Isolated Progress

//* Each route progresses independently.
//* Fully unlocking one route does **not** advance others.
//* Partial progress is always meaningful.
//* Retreating early is often the correct decision.

//This avoids tech-tree sludge and keeps geography relevant.

//---

//### 3. Exploration Is a Choice, Not a Mission

//There are **no exploration missions** or quest wrappers.

//Exploration happens when the player:

//*Chooses to sail into fog-of-war
//* Pushes beyond safe routes
//* Takes advantage of rare conditions (night, clear skies)

//The world never asks the player to explore.
//**The player decides to risk it.**

//---

//## Player-Facing Route Knowledge States

//These map to internal numeric progress but are shown narratively:

//1. * *No Known Route**

//   * No edge visible
//   * Fog feels absolute
//   * “The sea beyond is uncharted.”

//2. **Rumors of a Route**

//   * Faint / dashed / unstable edge
//   * Directional but unreliable
//   * Acquired via:

//     *Taverns
//     * Dockworkers
//     * Casual star observation on normal voyages
//     * Rumors, loot, NPCs

//3. **Partially Charted Route**

//   * Solid but visually unstable edge
//   * Travel is possible but risky or blocked
//   * Earned by deliberate exploration into fog

//4. **Known Route**

//   * Fully solid edge
//   * Normal travel rules apply
//   * Feels like relief, not excitement

//---

//## How Exploration Progress Is Earned

//### Charting Attempts (not missions)

//* Player sails away from known nodes into fog
//* Must be in the **boat scene**
//* Requires celestial conditions:

//  *Night
//  * Clear or acceptable skies
//  * Astrolabe or equivalent device
//* Player performs **Investigate Stars** interaction

//Each successful attempt grants **partial route progress**.

//---

//### Opportunistic Exploration (Key Feature)

//While on a normal voyage, an in-boat event may occur:

//> “The stars are unusually clear tonight…”

//Player choice:

//***Stay the course** (safe)
//* **Push onward into the unknown** (risk)

//Choosing risk:

//*Extends voyage distance/time
//* Increases danger (storms, enemies, damage)
//* Enables or boosts star charting progress

//Exploration becomes a **temptation**, not a task.

//---

//## Mini-Game Design Philosophy (Applies to All Mini-Games)

//### Core Rule

//**The mini-game is easy. The world makes it hard.**

//Mini-games never become mechanically complex.
//Failure comes from **external pressure**, not internal difficulty.

//---

//### Design Rules

//*Mechanically simple and repeatable
//* Always fail-able, never punishing
//* Player can always abort
//* Time always passes
//* Consequences come from the world, not the mini-game

//---

//### Investigate Stars – Mini-Game Pattern

//**Alignment + Drift**

//* Align celestial markers using an astrolabe-like interface
//*In calm conditions: slow, relaxing, almost meditative
//* Under pressure:

//  *Boat sway causes drift
//  * Waves cause sudden misalignment
//  * Weather obscures stars
//  * Damage increases instability
//  * Combat and alarms distract the player

//The task stays the same.
//**The context degrades.**

//---

//### Success & Failure Outcomes

//* **Clean success**: solid progress
//* **Messy success**: partial progress, more time passes
//* **Failure**:

//  *No progress this attempt
//  * Time passes
//  * Danger increases
//  * Situation worsens

//Failure still advances the world, never just a “nope”.

//---

//## Integration With Other Systems

//### Other Ways to Gain Star Knowledge

//* Dealers selling partial route knowledge
//* Looted charts from wrecks or pirates
//* Stolen or recovered information
//* Rumors that may be incomplete or wrong

//All feed into the same per-edge knowledge system.

//---

//### Relationship to Boat Upgrades

//Boat upgrades:

//* Reduce drift
//* Reduce interruption
//* Extend safe exploration range
//* Improve tolerance to bad conditions

//They **do not auto-unlock routes**.
//They make exploration* survivable*, not automatic.

//---

//## Design North Star (Emotional Target)

//> “This task is easy.
//> Doing it *right now* is not.”

//Players should fail because:

//* They were greedy
//* They waited too long
//* The sea didn’t cooperate
//* They pushed their luck

//Not because the mini-game was unfair.

//---

//## Why This Works

//* Discovery feels embodied and risky
//* Exploration competes with survival
//* Knowledge is fragile
//* Progress is earned through judgment
//* Curiosity is dangerous but tempting

//This system supports:

//* Long-term world progression
//* Emergent stories
//* Player-driven risk assessment
//* Replayability without grind

//---

//If you paste this back to me later, I’ll be able to pick up exactly where you left off without re-litigating any of it. And yes, this is genuinely strong design.
