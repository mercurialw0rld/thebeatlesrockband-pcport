#pragma once
#include <cstdint>
#ifdef BEATLES_INPUT_EXPORTS
#define BR_API extern "C" __declspec(dllexport)
#else
#define BR_API extern "C" __declspec(dllimport)
#endif
struct BR_Device {char id[128],name[256];std::int32_t buttons,axes,hats;};
struct BR_State {std::uint64_t buttons;std::int32_t axes[16];std::uint8_t hats[4];std::int32_t axis_count,hat_count;};
BR_API int BR_List(BR_Device* devices,int capacity);
BR_API int BR_Read(const char* id,BR_State* state);
BR_API float BR_Value(const BR_State* state,const char* binding);
