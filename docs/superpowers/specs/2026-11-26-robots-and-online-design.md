# Robot roster and online P2P play

## Why
Players pick robots that play differently (as university robot-hockey teams build different
robots for different jobs), and two players can play over a network without a server.

## Part 1: robots
- One `RobotSpec` asset per robot in `Assets/Resources/Robots/` (new robots = new asset, no code).
  Stats: speed (top speed x), weight (Rigidbody mass: shoving), size (body scale; bigger turns slower),
  strength (acceleration and how hard it hits the puck), tool, accent color, optional model prefab.
- Tools (one per robot; action key Space for blue, Enter for red):
  - Claw: Robockey-style shallow cradle; the puck sticks at low speed, slips on hard turns/hits; action shoots it.
  - Booster: short speed burst for ramming (cooldown).
  - Plow: wide V blade in front, hard to push sideways (passive).
  - Kicker: solenoid kick (cooldown).
  - Magnets: more grip, harder to shove (passive; strong on ice).
- Starting roster built from the existing model: Ômega (Claw, balanced), Faísca (Booster, light/fast),
  Trator (Plow, heavy/big), Canhão (Kicker), Ímã (Magnets).
- Robot select screen after setup: one slot per robot (1x1: 1 per team, 3x3: 3). 3x3 team weight budget
  (Caltech ME72 style): over the budget, the match can't start.
- The AI uses its robot's tool (kick/shoot when lined up, boost on long straight runs).

## Part 2: online (P2P by IP)
- Host/join by IP with Netcode for GameObjects + Unity Transport (UDP 7777). No accounts or relay.
- Host is authoritative (physics, AI, score); the client sends input and robot picks; host is blue,
  client is red, both full screen; 1x1 and 3x3.
- Internet play needs port forwarding on the host's router.

## Decided by the user
P2P by IP only; stats = speed, weight, size, tools, strength; tools Claw, Booster, Plow (+ Kicker,
Magnets proposed and accepted); research university robot hockey (UPenn Robockey, Caltech ME72).
