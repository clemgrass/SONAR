GODOT = godot-mono

SEED  ?= 20260811
PACE  ?= 1.5
SPEED ?= 1

.PHONY: run demo
run:
	$(GODOT) --path src/Sonar.Game

demo:
	$(GODOT) --path src/Sonar.Game -- --demo --seed=$(SEED) --pace=$(PACE) --speed=$(SPEED)
