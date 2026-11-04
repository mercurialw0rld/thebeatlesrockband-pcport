#include <windows.h>
#include <array>
#include <sstream>
#include <string>
#include <rex/cvar.h>
#include <rex/input/input.h>
#include "physical_input.h"
#include "controller_bridge.h"
#include "input_instrument.h"
REXCVAR_DEFINE_STRING(controller_device,"keyboard","Input","Physical controller identity selected in the launcher; keyboard selects keyboard input");
REXCVAR_DEFINE_STRING(controller_bindings,"","Input","Physical guitar action bindings supplied by the launcher");
namespace {
const std::array<const char*,15> actions={"green","red","yellow","blue","orange","strum_up","strum_down","whammy","star_power","solo","start","menu_left","menu_right","accept","cancel"};
constexpr std::array<std::uint16_t,15> buttons={0x1000,0x2000,0x8000,0x4000,0x100,1,2,0,0x20,0x200,0x10,4,8,0x1000,0x2000};
bool Read(PPCContext&ctx,BR_State&state){
 auto user=ctx.r3.u32,flags=ctx.r4.u32;
 if(user!=0&&(user&255)!=255&&!(flags&(1u<<30)))return false;
 return BR_Read(REXCVAR_GET(controller_device).c_str(),&state)!=0;
}
}
bool PhysicalInputCapabilities(PPCContext&ctx,std::uint8_t*base){
 if(REXCVAR_GET(controller_device).empty()||REXCVAR_GET(controller_device)=="keyboard")return false;
 BR_State state{};if(!ctx.r5.u32||!Read(ctx,state)){ctx.r3.u64=1167;return true;}
 auto&caps=*reinterpret_cast<rex::input::X_INPUT_CAPABILITIES*>(base+ctx.r5.u32);caps={};
 ConfigureInstrumentCaps(caps);ctx.r3.u64=0;return true;
}
bool PhysicalInputState(PPCContext&ctx,std::uint8_t*base){
 if(REXCVAR_GET(controller_device).empty()||REXCVAR_GET(controller_device)=="keyboard")return false;
 BR_State raw{};if(!ctx.r5.u32||!Read(ctx,raw)){ctx.r3.u64=1167;return true;}
 static const auto bindings=[](){std::array<std::string,15> result;std::istringstream stream(REXCVAR_GET(controller_bindings));std::string pair;while(std::getline(stream,pair,';')){auto split=pair.find('=');if(split==std::string::npos)continue;for(unsigned i=0;i<actions.size();i++)if(pair.substr(0,split)==actions[i])result[i]=pair.substr(split+1);}return result;}();
 auto&out=*reinterpret_cast<rex::input::X_INPUT_STATE*>(base+ctx.r5.u32);out={};
 static std::uint32_t packet=0;out.packet_number=++packet;out.gamepad.thumb_rx=DrumsSelected()?0:INT16_MIN;
 DWORD foreground=0;GetWindowThreadProcessId(GetForegroundWindow(),&foreground);
 if(foreground==GetCurrentProcessId()){
  std::uint16_t mask=0;for(unsigned i=0;i<actions.size();i++){if(DrumsSelected()&&(i==7||i==9))continue;float value=BR_Value(&raw,bindings[i].c_str());if(i==7)out.gamepad.thumb_rx=static_cast<std::int16_t>(-32768+value*65535);else if(value>.5f)mask|=buttons[i];}out.gamepad.buttons=mask;
 }
 ctx.r3.u64=0;return true;
}
