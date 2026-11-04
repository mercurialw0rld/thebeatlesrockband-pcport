// InputSystem::RefreshDevices mutates shared DeviceInfo vectors without a
// mutex in this SDK. TBRB polls it from multiple guest threads. All input
// imports used by this XEX share this lock, including the guitar caps hook.
#include <windows.h>
#include <atomic>
#include <rex/hook.h>
#include <rex/cvar.h>
#include <rex/input/input.h>
#include "input_lock.h"
#include "physical_input.h"
#include "input_instrument.h"
REXCVAR_DECLARE(bool, keyboard_guitar);
REXCVAR_DECLARE(bool, mnk_mode);
REXCVAR_DEFINE_BOOL(trace_guitar_input, false, "Diagnostics", "Log keyboard guitar input changes");
std::unique_lock<std::recursive_mutex> LockInputApi(){
 static std::recursive_mutex mutex;
 std::unique_lock lock(mutex,std::defer_lock);
 if(!lock.try_lock()){
  lock.lock();
  static std::atomic<bool> reported{false};
  if(!reported.exchange(true))REXLOG_INFO("Concurrent game input queries serialized to protect device list");
 }
 return lock;
}
namespace {
PPCFunc* OriginalInput(const char*name){
 auto*p=reinterpret_cast<PPCFunc*>(GetProcAddress(GetModuleHandleW(L"rexruntime.dll"),name));
 if(!p)REX_FATAL("Missing original input API {}",name);
 return p;
}
}
#define SERIAL_INPUT(name) REX_HOOK_RAW(name){static auto*original=OriginalInput(#name);auto lock=LockInputApi();original(ctx,base);}
REX_HOOK_RAW(__imp__XamInputGetState) {
 static auto* original=OriginalInput("__imp__XamInputGetState");
 auto lock=LockInputApi();
 if(PhysicalInputState(ctx,base))return;
 const auto user=ctx.r3.u32,flags=ctx.r4.u32,address=ctx.r5.u32;
 original(ctx,base);
 if(!REXCVAR_GET(keyboard_guitar)||!REXCVAR_GET(mnk_mode)||ctx.r3.u32!=0||!address)return;
 if(user!=0&&(user&0xffu)!=0xffu&&(flags&(1u<<30))==0)return;
 auto& state=*reinterpret_cast<rex::input::X_INPUT_STATE*>(base+address);
 // TBRB's strat_xbox configuration uses TRADITIONAL_WHAMMY_VAL:
 // -(RX + 1) / 2. A gamepad's centered RX=0 is already half a whammy.
 // The keyboard supplies a positive RX while held; the guitar rests at -1.
 state.gamepad.thumb_rx=DrumsSelected()?0:(int16_t(state.gamepad.thumb_rx)>0?INT16_MAX:INT16_MIN);
 if(REXCVAR_GET(trace_guitar_input)) {
  static int last_buttons=-1,last_whammy=0;
  const int buttons=uint16_t(state.gamepad.buttons),whammy=int16_t(state.gamepad.thumb_rx);
  if(buttons!=last_buttons||whammy!=last_whammy) {
   REXLOG_INFO("GUITAR input: buttons={:04X}, whammy={}, energy={}",buttons,whammy,(buttons&0x20)!=0);
   last_buttons=buttons;last_whammy=whammy;
  }
 }
}
SERIAL_INPUT(__imp__XamInputSetState)
