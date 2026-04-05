# Defect Archetypes Guide - Slay the Spire 2

> The Defect is a deeply technical, combo-reliant spellcaster. If you try brute force aggression, you'll get dismantled in Act 1. The character relies entirely on channeling elemental orbs that provide passive benefits every turn.

## Video References

📺 **[10 Easy TIPS For DEFECT BUILDS](https://www.youtube.com/watch?v=aRTRY3JD-nA)** - Comprehensive overview of all Defect strategies including orb manipulation, zero-cost builds, and energy management.

📺 **[This Defect Build Is Beyond OP](https://www.youtube.com/watch?v=BpXL0ProZGk)** - MythyMoo demonstrating powerful Defect combos.

---

## Character Overview

### Why Defect is Different
- Heavy reliance on the **Evoke mechanic**
- If randomized card rewards don't give reliable Evoke options, runs stall in Act 2
- Lacks consistent raw damage of Ironclad or explosive scaling of Regent
- When cards align perfectly = brilliant. When they don't = fragile robot waiting to be crushed.

### Core Identity (from video guide)
- **Most versatile character** — card draw, AoE damage, single target burst, random damage
- **Bias toward offense** — harder to get defense covered unless going all-in on Frost
- Versatility comes with complexity — many cards seem synergistic but don't work well together

### Unique Mechanics

**Orbs:**
- Channeled orbs sit in orb slots (default: 3 slots)
- Each orb has a **passive effect** (triggers each turn) and **evoke effect** (triggers when evoked)
- Orbs evoke when: slots fill (pushes oldest out), or card says "evoke"
- **Focus** increases orb effectiveness

**Orb Types:**
| Orb | Passive | Evoke | Notes |
|-----|---------|-------|-------|
| **Lightning** | Deal 3 damage to random enemy | Deal 8 damage | Can't control target (passive) |
| **Frost** | Gain 2 Block | Gain 5 Block | THE BEST ORB. Block engine. |
| **Dark** | Gain 6 damage (stacks) | Deal all accumulated damage | Boss killer. Builds over time. |
| **Plasma** | Gain 1 Energy | Gain 2 Energy | Energy generation |
| **Glass** (NEW) | Deal 5 AoE damage, lose 1 damage | Deal all remaining damage as AoE | AoE that ticks down |

**Focus:**
- Increases orb passive/evoke values
- +1 Focus = Frost blocks 3 instead of 2, Lightning deals 4 instead of 3, etc.
- **Temporary Focus** is common in STS2 (Hot Fix, Focus Strike, Synchronize)
- **Permanent Focus** is rare (Defragment only at rare)

### Starter Deck (10 cards)
```
Strike x4
Defend x4
Zap x1 (1 cost, channel 1 Lightning)
Dualcast x1 (1 cost, evoke rightmost orb twice)
```

### Starting Relic
**Cracked Core** — Channel 1 Lightning at start of combat. Massive tempo advantage — passive damage before you even draw.

---

## Primary Archetypes

### 1. Frost Focus (S-Tier — Dominant Strategy)

**The strategy that carried STS1 is even easier in STS2. Game doesn't punish slow play like STS1 Heart did.**

### Why Frost is Broken
1. Frost orbs block EVERY turn passively
2. With Focus, they block even more
3. Evoking Frost = even more block
4. Multiple Frost orbs = can't take damage
5. Game doesn't punish slow play like STS1 Heart did

### Core Frost Cards

| Card | Tier | Energy | Effect |
|------|------|--------|--------|
| **Glacier** | S | 2 | 6 Block, channel 2 Frost |
| **Coolheaded** | S | 1 | Channel 1 Frost, draw 1 card |
| **Chill** | S | 0 | Channel 1 Frost per enemy, Exhaust |
| **Cold Snap** | A | 1 | Deal 6 damage, channel 1 Frost |
| **Ice Lance** | A | 3 | Deal 19 damage, channel 3 Frost |

### Focus Sources

**Permanent (Rare):**
- **Defragment** (S) — 1 Focus permanent. Best focus card.

**Temporary (Common/Uncommon):**
- **Hot Fix** (S) — 0 cost, +2 Focus this turn (NERFED: now exhausts)
- **Focus Strike** (A) — 1 cost, 9 damage + 1 Focus this turn
- **Synchronize** (S) — +2 Focus per unique orb type

### The Gameplan
1. Pick Frost cards early (Coolheaded, Cold Snap, Glacier)
2. Pick temp focus (Hot Fix, Focus Strike)
3. Get Capacitor or Modded for more orb slots
4. Block every hallway, farm fights safely
5. Add Dark orbs for boss damage (Darkness, Shadow Shield)
6. Win

---

### 2. Dark Orb Boss Killing (A-Tier)

**When Frost handles defense, Dark handles offense. The perfect complement.**

### How Dark Orbs Work
- Passive: Gain 6 damage (stacks each turn)
- Evoke: Deal ALL accumulated damage to enemy
- With Focus: Gain 6 + Focus per turn

### Key Dark Cards

| Card | Tier | Energy | Effect |
|------|------|--------|--------|
| **Darkness** | S | 1 | Channel 1 Dark, trigger all Dark passives |
| **Shadow Shield** | A | 2 | 11 Block, channel 1 Dark |
| **Null** | A | 2 | 10 damage, 2 Weak, channel 1 Dark |
| **Rainbow** | B | 2 | Channel Lightning, Frost, Dark |
| **Quadcast** | A | 1 | Evoke rightmost orb 4 times (Ancient) |

### Dark Strategy
1. Channel Dark orbs early in fight
2. Let them cook (6+ damage per turn)
3. Block with Frost while waiting
4. Dualcast/Quadcast/Multi-Cast to evoke for massive damage
5. One-shot boss

**Combo:** Darkness → Darkness → Dark has 18+ damage → Dualcast = 36+ damage

---

### 3. Claw / Zero-Cost Build (B-Tier)

**The meme archetype. Legendary and fun but inconsistent.**

📺 Video insight: "Zero energy attacks really love anything that gives you more card draw and cycling. Hologram becomes very powerful once upgraded — bounce cards back from graveyard back and forth."

### The Paradox
- Need 0-cost cards AND draw AND payoffs
- 0-cost cards don't do much individually
- Too many 0-costs = float energy
- Draw-order dependent

### Key Claw Cards

| Card | Tier | Energy | Effect |
|------|------|--------|--------|
| **Claw** | C | 0 | Deal 3, +2 to all Claws this combat |
| **All for One** | A | 2 | Deal 10, get all 0-cost from discard |
| **FTL** | A | 0 | Deal 5, draw 1 if <3 cards played |
| **Scrape** | D | 1 | Draw 4, discard non-0-cost (dangerous!) |
| **Beam Cell** | C | 0 | Deal 3, apply 1 Vulnerable |
| **Hologram** | A | 1 | Get card from discard, can bounce repeatedly |

### Claw Strategy
1. Get draw first (Coolheaded, Machine Learning)
2. Add 0-cost cards (FTL, Go for the Eyes, Hot Fix)
3. Find All for One
4. Loop: Play 0-costs → All for One → repeat
5. Use Frost for defense (orbs not needed for damage here)

**Warning:** Don't take Scrape early — it can discard your Echo Form!

---

### 4. Status / Turbo Build (C-Tier)

**From video guide: "Defect has ways to utilize status effects directly and weaponize them. Compact transforms status cards into fuel."**

### How It Works
- Generate status cards (Turbo, Fight Through, Boost Away)
- Status payoffs (Compact, Rocket Punch, Iteration, Flak Cannon)
- Convert bad cards into value

### Key Status Cards

| Card | Effect | Notes |
|------|--------|-------|
| **Turbo** | Gain 2 energy, add Void | Bridge card. Void exhausts itself. |
| **Fight Through** | 13 Block, add 2 Wounds | Power Through on Defect |
| **Compact** | 6 Block, transform statuses to Fuel | Fuel = energy or draw. Best status card. |
| **Rocket Punch** | 13 damage, costs 0 if status created | Main payoff |
| **Iteration** | First status drawn = draw 2 | Evolve but limited |
| **Flak Cannon** | Removes statuses, turns to attack | Mass removal |
| **Trash to Treasure** | Convert statuses to weapons | Synergy card |

### Why It's Weak
- Need bridge cards BEFORE payoffs
- Bridge cards are bad on their own
- Frost Focus is just easier
- Status cards hurt your second cycle

**Only go status if:** You transform into status payoffs early and can't find Frost.

---

### 5. Lightning Spam (B-Tier)

**New in STS2 with Voltaic.**

### How It Works
- Channel lots of Lightning
- Voltaic = channel Lightning equal to Lightning channeled this combat
- Goes exponential (2 → 4 → 8 → 16...)

### Key Lightning Cards

| Card | Tier | Effect |
|------|------|--------|
| **Voltaic** | A | Channel Lightning = total Lightning this combat |
| **Thunder** | B | Evoke Lightning = deal 6 to all enemies hit |
| **Ball Lightning** | B | Deal 7, channel 1 Lightning |
| **Lightning Rod** | B | 4 Block for 3 turns, channel 1 Lightning |
| **Tempest** | C | Channel X Lightning |

### Lightning Strategy
1. Get Voltaic early
2. Channel Lightning naturally (Ball Lightning, starter Zap)
3. Voltaic doubles your Lightning count
4. Thunder makes evokes deal AoE
5. Boss dies to Lightning spam

**Note:** Frost is still your block engine. Lightning is supplemental damage.

---

### 6. Power Spam (B-Tier)

**From video guide: "Defect is one of the few characters that can snowball just on powers alone."**

### How It Works
- Creative AI generates powers
- Echo Form duplicates plays
- Make powers cost-free eventually
- Snowball with passive effects

### Key Power Cards
| Card | Effect |
|------|--------|
| **Echo Form** | First card each turn played twice |
| **Creative AI** | Add random power to hand each turn |
| **Heatsinks** | Draw 1 when you play a power |
| **Storm** | Channel Lightning when you play a power |
| **Machine Learning** | Draw +1 card each turn |

### Warning from video:
"Power spamming is risky because it bloats your deck. Every power you draft makes your build less reliable. Powers disappear once played, but you have to draw them first — that means higher energy cost and less block cards drawn in critical turns."

**Only go powers if:** You see Echo Form + Creative AI early and can support with draw.

---

## Energy Manipulation (Video Highlight)

**From video guide: "The Defect has several ways to mitigate costs entirely and create energy. Costs are very high — you always require something that generates more energy."**

### Key Energy Cards
| Card | Effect |
|------|--------|
| **Turbo** | +2 energy, add Void |
| **Aggregate** | +1 energy per orb |
| **Overclock** | Draw 2, add Burn (Burn is acceptable) |
| **Double Energy** | Double your energy |
| **Momentum Strike** | Costs 0 after first play |
| **Rocket Punch** | Costs 0 if status created |

**Tip:** Even if you have too much energy, there are good skills to funnel it into (X-cost cards like Tempest, Reinforced Body).

---

## Act-by-Act Priorities

### Act 1: Easiest Act
- Defect has strong Act 1 with passive orb damage
- Pick Frost cards (Coolheaded, Cold Snap, Glacier)
- Pick block cards (Charge Battery, Leap, Boot Sequence)
- One Lightning orb from Cracked Core handles most hallway damage
- Temp focus (Hot Fix, Focus Strike) if you see it

### Act 2: Continue Building
- Add orb slots (Capacitor, Modded)
- Add Dark orbs for boss damage (Darkness, Shadow Shield)
- Add permanent focus if you see Defragment
- Remove Strikes/Defends
- Frost should be handling all block by now

### Act 3: Victory Lap
- If you have Frost + Focus, you don't take damage
- Add Dark orbs or Voltaic for boss scaling
- Echo Form if offered (double everything)
- Game doesn't challenge Defect like STS1 Heart did

---

## Block Situation (Video Highlight)

**From video guide: "Block comes by a little bit harder for Defect. Most power budget is linked to orb gameplay. Focus increases defensive qualities better than Dexterity if running Frost orbs."**

### Best Block Cards
| Card | Effect | Notes |
|------|--------|-------|
| **Charge Battery** | 7 block, +1 energy next turn | Excellent cost/effect |
| **Boost Away** | 0 cost, 8 block, add status | Best 0-cost block |
| **Fight Through** | 13 block, add 2 Wounds | High block value |
| **Glacier** | 6 block + 2 Frost | Block + orb scaling |
| **Leap** | 9 block | Simple and good |

**Solutions to block problem:**
1. Lots of card draw + energy → spam block cards
2. Focus + Frost orbs → passive block scaling
3. Don't take too much damage and race enemies down

---

## Upgrade Priority

| Priority | Cards |
|----------|-------|
| **High** | Coolheaded (draw 2), Dualcast (0 cost), Echo Form (no Ethereal), Hot Fix (no Exhaust) |
| **Medium** | Glacier (+3 block), Defragment (+1 Focus), Zap (0 cost) |
| **Low** | Damage cards (orbs handle damage) |

**Key Upgrades:**
- **Coolheaded:** Draw 1 → Draw 2 (huge)
- **Dualcast:** 1 cost → 0 cost (play with other stuff)
- **Hot Fix:** Exhausts → Doesn't exhaust (loop it)

---

## Relic Priorities

### S-Tier for Defect
- **Inserter** — Gain 1 orb slot every 2 turns
- **Data Disk** — +1 Focus
- **Runic Capacitor** — +3 orb slots

### Good Synergies
- Energy relics (play more powers)
- Draw relics (find temp focus)
- Block relics (supplement Frost)

### Avoid
- **Velvet Choker** — 6 card limit hurts Claw/Coolheaded spam
- **Runic Dome** — Can't see attacks (less relevant with perma-block)

---

## Common Mistakes

1. **Picking Claw early** — It's a meme. Frost Focus wins more.
2. **Ignoring Dark orbs** — You need boss damage. Frost alone is slow.
3. **Taking status cards without payoffs** — Bridge cards are bad alone.
4. **Not upgrading Coolheaded** — Draw 2 vs Draw 1 is huge.
5. **Picking Scrape** — It can discard your Echo Form. Dangerous.
6. **Too many powers** — Powers brick turn 1. Balance with block.
7. **Mixing too many archetypes** — Pick a lane (video advice)

---

## Archetype Tier Summary

| Tier | Archetype | Notes |
|------|-----------|-------|
| **S** | Frost Focus | Easy mode. Passive block wins. |
| **A** | Dark Orbs | Boss killer. Pairs with Frost. |
| **B** | Lightning Spam | Voltaic makes this viable. Still need Frost. |
| **B** | Power Spam | Can snowball but risky. Bloats deck. |
| **B** | Claw/Zero-Cost | Fun meme. Draw-dependent. |
| **C** | Status Build | Need payoffs before bridges. Hard to assemble. |

---

## Quick Reference: Card Priorities

**Always Pick (S-tier):**
- Echo Form, Glacier, Coolheaded, Defragment, Capacitor, Chill

**Usually Pick (A-tier):**
- Cold Snap, Boot Sequence, Charge Battery, Darkness, Focus Strike, FTL

**Situational (B-tier):**
- Ball Lightning, Turbo, Skim, Creative AI, All for One

**Rarely Pick (C/D-tier):**
- Claw, Beam Cell, Status cards (without payoffs), Scrape

**Never Pick:**
- Gunk Up, Chaos, Trash to Treasure (without setup), Rip and Tear

---

*Last updated: March 2026 — Early Access meta*
