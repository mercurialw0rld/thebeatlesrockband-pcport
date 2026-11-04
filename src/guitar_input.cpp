// Present the keyboard-backed player-one pad as an XInput guitar.
// Frets and strum remain simultaneous inputs from the SDK keyboard driver.
#include <windows.h>
#include <atomic>
#include <rex/assert.h>
#include <rex/cvar.h>
#include <rex/hook.h>
#include <rex/input/input.h>
#include "input_lock.h"
#include "physical_input.h"
#include "input_instrument.h"

REXCVAR_DEFINE_BOOL(keyboard_guitar, true, "Input", "Present player-one keyboard as a guitar");
REXCVAR_DEFINE_STRING(input_instrument, "guitar", "Input", "Player-one instrument: guitar or drums");

namespace {
PPCFunc* OriginalCapabilities() {
  static auto* original = reinterpret_cast<PPCFunc*>(GetProcAddress(
      GetModuleHandleW(L"rexruntime.dll"), "__imp__XamInputGetCapabilities"));
  if (!original) REX_FATAL("The runtime has no original XamInputGetCapabilities export");
  return original;
}
}

REX_HOOK_RAW(__imp__XamInputGetCapabilities) {
  auto input_lock = LockInputApi();
  if(PhysicalInputCapabilities(ctx,base))return;
  const std::uint32_t user = ctx.r3.u32;
  const std::uint32_t flags = ctx.r4.u32;
  const std::uint32_t caps_address = ctx.r5.u32;
  OriginalCapabilities()(ctx, base);
  const bool player_one = user == 0 || (user & 0xffu) == 0xffu || (flags & (1u << 30));
  if (!REXCVAR_GET(keyboard_guitar) || !player_one || !caps_address ||
      ctx.r3.u32 != 0) return;

  auto* caps = reinterpret_cast<rex::input::X_INPUT_CAPABILITIES*>(base + caps_address);
  ConfigureInstrumentCaps(*caps);
  static std::atomic<bool> reported{false};
  if (!reported.exchange(true)) {
    REXLOG_INFO("Keyboard instrument enabled: instrument={}, subtype={}, configurable pads/frets and menu", DrumsSelected()?"drums":"guitar", DrumsSelected()?8:6);
  }
}



