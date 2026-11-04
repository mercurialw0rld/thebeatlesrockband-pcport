#pragma once
#include <windows.h>
#include <combaseapi.h>

// Audio clients may be created and destroyed by different guest threads.
// Keep the MTA alive for the entire engine lifetime without balancing a
// thread-local CoInitializeEx on the wrong thread or changing an existing STA.
class WindowsAudioCom final {
 public:
  WindowsAudioCom() = default;
  WindowsAudioCom(const WindowsAudioCom&) = delete;
  WindowsAudioCom& operator=(const WindowsAudioCom&) = delete;
  ~WindowsAudioCom() noexcept {
    if (cookie_) CoDecrementMTAUsage(cookie_);
  }
  HRESULT Initialize() noexcept {
    return cookie_ ? S_FALSE : CoIncrementMTAUsage(&cookie_);
  }
 private:
  CO_MTA_USAGE_COOKIE cookie_ = nullptr;
};
