#pragma once
#include <rex/cvar.h>
#include <rex/input/input.h>
REXCVAR_DECLARE(std::string, input_instrument);
inline bool DrumsSelected() { return REXCVAR_GET(input_instrument) == "drums"; }
inline void ConfigureInstrumentCaps(rex::input::X_INPUT_CAPABILITIES& caps) {
  caps = {};
  caps.type = rex::input::XINPUT_DEVTYPE_GAMEPAD;
  caps.sub_type = DrumsSelected() ? rex::input::XINPUT_DEVSUBTYPE_DRUM_KIT
                                 : rex::input::XINPUT_DEVSUBTYPE_GUITAR;
  caps.gamepad.buttons = DrumsSelected() ? 0xf13f : 0xf33f;
  if (!DrumsSelected()) caps.gamepad.thumb_rx = INT16_MAX;
}
