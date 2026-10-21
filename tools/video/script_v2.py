"""Voiceover v2: phrases with pace, emphasis and timing.

Each phrase: (id, text, speed, emphasis, gap_before_s, at_s)
- speed: Kokoro speed (1.0 normal; key phrases slower)
- emphasis: rendered alone, then lifted ~3.5 % in pitch and +2.5 dB
- at_s: earliest start on the video timeline (an anchor to a picture cue); otherwise it follows the
  previous phrase after gap_before_s.
Picture cues (v2 cut): amber 46.0 s, CNC failure 59.3 s, tool change 78.0 s, end card 101 s.
"""

PHRASES = [
    # Intro: start card, then the hall dolly
    ("I1", "A digital twin is a live, virtual copy of a real machine, or a whole production line.", 0.95, False, 0, 1.2),
    ("I2", "It runs on the same data as the factory floor,", 1.0, False, 0.5, None),
    ("I3", "so you can watch it, test it, and predict what happens next,", 1.0, False, 0.2, None),
    ("I4", "without touching the real thing.", 0.95, True, 0.25, None),
    # The cell
    ("C1", "This one is a single production cell:", 1.0, False, 0.35, None),
    ("C2", "an infeed conveyor, a sensor gate, a robot arm,", 1.0, False, 0.25, None),
    ("C3", "and a CNC machining centre.", 0.95, True, 0.15, None),
    ("C4", "Every part of it streams live data.", 1.0, False, 0.5, None),
    # The use case
    ("P1", "That data powers one of the most valuable uses of a twin in manufacturing:", 1.0, False, 0.7, None),
    ("P2", "predictive maintenance.", 0.88, True, 0.3, None),
    ("P3", "This machining centre is going to fail.", 0.95, False, 0.8, None),
    ("P4", "Its twin knows when.", 0.9, True, 0.5, None),
    # It sees it coming (amber at 44.5 s)
    ("W1", "The tool is wearing.", 1.0, False, 0, 39.6),
    ("W2", "The twin measures how close every reading is to the machine's failure limits.", 1.0, False, 0.35, None),
    ("W3", "Risk climbs.", 0.9, True, 0, 45.3),
    ("W4", "The light turns amber.", 0.9, True, 0.3, None),
    ("W5", "And it tells you to change the tool,", 1.0, False, 0.5, None),
    ("W6", "while the line is still running.", 1.0, False, 0.1, None),
    # Ignore it (failure at 56.8 s)
    ("F1", "Ignore it,", 1.0, False, 0, 57.0),
    ("F2", "and the tool fails.", 0.9, True, 0.15, None),
    ("F3", "The machine stops.", 1.0, False, 1.0, None),
    ("F4", "The robot finishes its pick.", 1.0, False, 0.3, None),
    ("F5", "Both conveyors halt.", 1.0, False, 0.3, None),
    ("F6", "And the twin counts every part you're losing.", 0.95, True, 0.4, None),
    # Act on it (tool change at 75.5 s)
    ("A1", "Act on it instead.", 0.9, True, 0, 72.6),
    ("A2", "One tool change, at the recommendation:", 1.0, False, 0.6, None),
    ("A3", "wear resets,", 1.0, False, 0, 77.9),
    ("A4", "risk drops back to zero,", 1.0, False, 0.1, None),
    ("A5", "and the line never stops.", 1.0, False, 0.1, None),
    ("A6", "Zero parts lost.", 0.88, True, 0.7, None),
    # Your line
    ("Y1", "Your line works the same way.", 1.0, False, 0, 87.6),
    ("Y2", "We connect to your machines over M Q T T, or O P C U A,", 1.0, False, 0.4, None),
    ("Y3", "set their limits from your maintenance history,", 1.0, False, 0.15, None),
    ("Y4", "and build the twin around them.", 0.95, True, 0.15, None),
    # End card (from 98.5 s)
    ("E1", "See your line stop,", 0.92, True, 0, 101.8),
    ("E2", "before it does.", 0.92, True, 0.1, None),
    ("E3", "Book a call at two dude studios dot com, slash manufacturing.", 0.95, False, 0.7, None),
]

TOTAL_S = 111.0
