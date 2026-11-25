GODOT = godot-mono

SEED  ?= 20260811
PACE  ?= 1.5
SPEED ?= 1
BUILD ?= build

.PHONY: run demo export export-linux export-windows embed-config

run:
	$(GODOT) --path src/Sonar.Game

demo:
	$(GODOT) --path src/Sonar.Game -- --demo --seed=$(SEED) --pace=$(PACE) --speed=$(SPEED)

# El build no ve config/ de la raíz: se copia dentro del proyecto y el preset la empaqueta.
embed-config:
	cp config/simulation.working.json src/Sonar.Game/simulation.json

export: export-linux export-windows

export-linux: embed-config
	mkdir -p $(BUILD)/linux
	$(GODOT) --headless --path src/Sonar.Game --export-release "Linux" $(abspath $(BUILD))/linux/Sonar.x86_64

export-windows: embed-config
	mkdir -p $(BUILD)/windows
	$(GODOT) --headless --path src/Sonar.Game --export-release "Windows Desktop" $(abspath $(BUILD))/windows/Sonar.exe
