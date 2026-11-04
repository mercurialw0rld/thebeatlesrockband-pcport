// Exercise the production driver, not a duplicate initialization sequence.
#include <windows.h>
#include <xaudio2.h>
#include <mmdeviceapi.h>
#include <atomic>
#include <cstdio>
#include <memory>
#include <stdexcept>
#include <thread>

static std::atomic<bool> fail_engine{false};
static HRESULT TestXAudio2Create(IXAudio2** engine, UINT32 flags, XAUDIO2_PROCESSOR processor) {
  if (fail_engine.load()) { *engine = nullptr; return E_FAIL; }
  return XAudio2Create(engine, flags, processor);
}
#define XAudio2Create TestXAudio2Create
#include "../src/windows_audio.cpp"
#undef XAudio2Create
REXCVAR_DEFINE_BOOL(trace_audio_frames, false, "Tests", "No performance tracing");

static int checks = 0;
static void Check(bool ok, const char* label) {
  if (!ok) throw std::runtime_error(label);
  ++checks; std::printf("PASS %s\n", label);
}
static APTTYPE Apartment() {
  APTTYPE type; APTTYPEQUALIFIER qualifier;
  Check(SUCCEEDED(CoGetApartmentType(&type, &qualifier)), "COM apartment is available");
  return type;
}
int main(int argc, char** argv) {
  SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX);
  _set_abort_behavior(0, _WRITE_ABORT_MSG | _CALL_REPORTFAULT);
  rex::cvar::Init(argc, argv);
  if (argc == 2 && std::string(argv[1]) == "--fail-driver") {
    rex::InitLogging("windows-audio-test-failure.log");
    fail_engine = true;
    auto driver = CreateWindowsAudioDriver(nullptr, nullptr);
    return 99;  // Must stop at the audio error, never return a null guest handle.
  }
  try {
    IMMDeviceEnumerator* enumerator = nullptr;
    HRESULT before = CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr,
        CLSCTX_INPROC_SERVER, __uuidof(IMMDeviceEnumerator), reinterpret_cast<void**>(&enumerator));
    if (enumerator) enumerator->Release();
    Check(before == CO_E_NOTINITIALIZED, "fresh process reproduces missing COM prerequisite");
    {
      WindowsAudioDriver driver(nullptr, nullptr);
      Check(driver.Initialize(), "production XAudio2 driver starts without prior COM initialization");
      Check(Apartment() == APTTYPE_MTA, "audio owns a live implicit MTA");
    }
    std::exception_ptr error;
    auto run = [&](auto fn) {
      std::thread worker([&] { try { fn(); } catch (...) { error = std::current_exception(); } });
      worker.join(); if (error) std::rethrow_exception(error);
    };
    run([] {
      Check(SUCCEEDED(CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED)), "existing STA setup");
      const auto caller_apartment = Apartment();
      Check(caller_apartment == APTTYPE_STA || caller_apartment == APTTYPE_MAINSTA, "caller is in an STA apartment");
      {
        WindowsAudioDriver driver(nullptr, nullptr);
        Check(driver.Initialize(), "production driver accepts an existing STA caller");
        Check(Apartment() == caller_apartment, "audio does not change the caller STA");
      }
      Check(Apartment() == caller_apartment, "audio cleanup does not uninitialize the caller STA");
      CoUninitialize();
    });
    run([] {
      Check(SUCCEEDED(CoInitializeEx(nullptr, COINIT_MULTITHREADED)), "existing MTA setup");
      { WindowsAudioDriver driver(nullptr, nullptr); Check(driver.Initialize(), "production driver accepts an existing MTA caller"); }
      Check(Apartment() == APTTYPE_MTA, "audio cleanup preserves caller-owned MTA");
      CoUninitialize();
    });
    std::unique_ptr<WindowsAudioDriver> transferred;
    run([&] {
      transferred = std::make_unique<WindowsAudioDriver>(nullptr, nullptr);
      Check(transferred->Initialize(), "create audio on a short-lived guest thread");
    });
    run([&] {
      Check(Apartment() == APTTYPE_MTA, "MTA outlives the creating guest thread");
      transferred.reset(); Check(true, "destroy audio safely on a different guest thread");
    });
    {
      WindowsAudioCom first, second;
      Check(SUCCEEDED(first.Initialize()) && first.Initialize() == S_FALSE,
          "repeated scope initialization does not acquire an extra cookie");
      Check(SUCCEEDED(second.Initialize()), "independent clients acquire independent MTA lifetimes");
    }
    fail_engine = true;
    { WindowsAudioDriver driver(nullptr, nullptr); Check(!driver.Initialize(), "engine creation failure returns safely without touching null voices"); }
    fail_engine = false;
    { WindowsAudioDriver driver(nullptr, nullptr); Check(driver.Initialize(), "audio can initialize again after an engine failure"); }
    std::printf("%d audio checks passed\n", checks);
    return 0;
  } catch (const std::exception& error) {
    std::printf("FAIL %s\n", error.what()); return 1;
  }
}
