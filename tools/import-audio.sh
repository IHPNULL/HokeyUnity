#!/usr/bin/env bash
# Re-imports the free Unity Asset Store audio the game uses (Resources/Audio/SoundBank).
# The packs are not in git: the Asset Store license allows them inside the built game, not as
# loose files in a public repository. Needs the Unity CLI signed in (unity auth login).
# Without them the game still runs with its synthesized sounds.
set -euo pipefail
cd "$(dirname "$0")/.."
ids=(
  01a1c3b4-d19b-755a-b874-8dcfe57db86f   # Free Action Track 1 (Loop)
  01a1c3b4-d19b-7567-bf5e-e3229265ed32   # Free Action Track 2 (Loop)
  01a1a8ae-97bb-707c-b68a-21c21b368a20   # Gregor Quendel - Free Crowd Cheering Sounds - 01 - Strong cheering and strong rhythmic cheering
  01a1a8ae-97bb-707d-bea5-20b53a73c408   # Gregor Quendel - Free Crowd Cheering Sounds - 10 - Ambience
  01a1a91d-1b86-7849-b50a-bb6d07df84dd   # Horn_1_a
  01a1a91d-1b86-7877-9c16-000e69ed0816   # Horn_1_c
  01a1a91d-1b86-781b-b33b-c3e41d2b1d3f   # beep_3_c
  01a1a91d-1b86-786f-8e68-fb4a743e41e8   # beep_2_a
  01a1c3b8-97f4-7875-a937-cb90e8afd2bc   # Metal_Hit-013
  01a1c3b8-97f4-78ca-8d36-a5421a0501ac   # Metal_Hit-014
  01a1a877-562c-74e7-a182-758595dea456   # Mech Game Item hit 1
  01a1a877-562c-74a0-aedf-4c37a536987e   # Mech Game Item hit 5
  01a1a877-562c-74e9-adb9-6f2ca2cb47f9   # Mech Game Item hit 14
  01a1a877-562c-74b6-a051-75701dae3fb9   # Mech Game Item hit 12
  01a1a92c-8631-759f-9414-aee8701cfbcb   # Metal_46
  01a1a92c-8631-75a0-a11f-a20c950b00f2   # Metal_41
)
for id in "${ids[@]}"; do unity assetlibrary import "$id" --non-interactive; done
