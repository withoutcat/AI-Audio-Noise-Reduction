// NoiseReduction.Bridge - C++ bridge DLL for Agora RTC SDK
// Exports C functions for C# P/Invoke to control AI noise suppression

#include "IAgoraRtcEngine.h"
#include "IAgoraMediaEngine.h"
#include "IAudioDeviceManager.h"
#include "AgoraBase.h"
#include "AgoraMediaBase.h"

#include <windows.h>
#include <stdio.h>

using namespace agora::rtc;
using namespace agora::media;
using namespace agora::base;

// ============================================================
// Global state
// ============================================================
static IRtcEngine* g_engine = nullptr;
static IMediaEngine* g_mediaEngine = nullptr;
static IAudioDeviceManager* g_audioDeviceManager = nullptr;

// C callback type: called from unmanaged code when a PCM frame is ready
// Parameters: buffer, samplesPerChannel, channels, sampleRate, bytesPerSample, userData
typedef void (__stdcall *AudioFrameCallback)(
    const void* buffer,
    int samplesPerChannel,
    int channels,
    int sampleRate,
    int bytesPerSample,
    void* userData);

static AudioFrameCallback g_audioCallback = nullptr;
static void* g_audioCallbackUserData = nullptr;

// ============================================================
// Fake event handler (minimal implementation)
// ============================================================
class FakeEventHandler : public IRtcEngineEventHandler {
public:
    void onJoinChannelSuccess(const char* channelId, uid_t uid, int elapsed) override {
        printf("[Bridge] Joined channel: %s, uid: %u\n", channelId, uid);
    }
    void onLeaveChannel(const RtcStats& stats) override {
        printf("[Bridge] Left channel\n");
    }
    void onError(int err, const char* msg) override {
        printf("[Bridge] Error: %d - %s\n", err, msg ? msg : "(null)");
    }
};

static FakeEventHandler g_eventHandler;

// ============================================================
// Audio frame observer - receives降噪后的 PCM data from SDK
// ============================================================
class BridgeAudioObserver : public IAudioFrameObserver {
public:
    // Tell SDK we only want to observe recorded (local mic) audio
    int getObservedAudioFramePosition() override {
        return AUDIO_FRAME_POSITION_RECORD;  // 0x0002
    }

    // Set audio format for recording callback
    AudioParams getRecordAudioParams() override {
        // 16kHz, mono, read-write mode, 1024 samples per call
        return AudioParams(16000, 1, RAW_AUDIO_FRAME_OP_MODE_READ_WRITE, 1024);
    }

    AudioParams getPlaybackAudioParams() override {
        return AudioParams();  // default (not used)
    }

    AudioParams getMixedAudioParams() override {
        return AudioParams();  // default (not used)
    }

    AudioParams getEarMonitoringAudioParams() override {
        return AudioParams();  // default (not used)
    }

    // This is the key callback - receives降噪后的 PCM audio frames
    bool onRecordAudioFrame(const char* channelId, AudioFrame& audioFrame) override {
        if (g_audioCallback && audioFrame.buffer) {
            g_audioCallback(
                audioFrame.buffer,
                audioFrame.samplesPerChannel,
                audioFrame.channels,
                audioFrame.samplesPerSec,
                (int)audioFrame.bytesPerSample,
                g_audioCallbackUserData
            );
        }
        return true;
    }

    bool onPlaybackAudioFrame(const char* channelId, AudioFrame& audioFrame) override {
        return true;  // not used
    }

    bool onMixedAudioFrame(const char* channelId, AudioFrame& audioFrame) override {
        return true;  // not used
    }

    bool onEarMonitoringAudioFrame(AudioFrame& audioFrame) override {
        return true;  // not used
    }

    bool onPlaybackAudioFrameBeforeMixing(const char* channelId, uid_t uid, AudioFrame& audioFrame) override {
        return true;  // not used
    }
};

static BridgeAudioObserver g_audioObserver;

// ============================================================
// Exported C functions
// ============================================================

extern "C" {

// Initialize the RTC engine with the given App ID
// Returns 0 on success, negative on failure
__declspec(dllexport) int __cdecl Bridge_Init(const char* appId) {
    if (g_engine) {
        printf("[Bridge] Engine already initialized\n");
        return 0;
    }

    printf("[Bridge] Creating RTC engine...\n");
    g_engine = createAgoraRtcEngine();
    if (!g_engine) {
        printf("[Bridge] Failed to create RTC engine\n");
        return -1;
    }

    RtcEngineContext ctx;
    memset(&ctx, 0, sizeof(ctx));
    ctx.eventHandler = &g_eventHandler;
    ctx.appId = appId;
    ctx.autoRegisterAgoraExtensions = true;  // CRITICAL: must be true for AI降噪 extension to load

    printf("[Bridge] Initializing with AppID: %s\n", appId ? appId : "(null)");
    int ret = g_engine->initialize(ctx);
    if (ret != 0) {
        printf("[Bridge] Initialize failed: %d\n", ret);
        g_engine->release();
        g_engine = nullptr;
        return ret;
    }

    printf("[Bridge] Engine initialized successfully\n");

    // Get media engine for audio observer registration
    int qret = g_engine->queryInterface(AGORA_IID_MEDIA_ENGINE, (void**)&g_mediaEngine);
    if (qret != 0 || !g_mediaEngine) {
        printf("[Bridge] queryInterface for media engine failed: %d\n", qret);
        // Non-fatal for now, we'll retry later
    } else {
        printf("[Bridge] Media engine obtained\n");
    }

    // Get audio device manager for device selection
    qret = g_engine->queryInterface(AGORA_IID_AUDIO_DEVICE_MANAGER, (void**)&g_audioDeviceManager);
    if (qret != 0 || !g_audioDeviceManager) {
        printf("[Bridge] queryInterface for audio device manager failed: %d\n", qret);
        // Non-fatal, device selection will use default
    } else {
        printf("[Bridge] Audio device manager obtained\n");
    }

    return 0;
}

// Enable/disable AI noise suppression
// enabled: 1=on, 0=off
// mode: 0=balanced, 1=aggressive, 2=ultralowlatency
__declspec(dllexport) int __cdecl Bridge_SetAINSMode(int enabled, int mode) {
    if (!g_engine) {
        printf("[Bridge] Engine not initialized\n");
        return -1;
    }

    printf("[Bridge] Setting AINS mode: enabled=%d, mode=%d\n", enabled, mode);
    int ret = g_engine->setAINSMode(enabled != 0, (AUDIO_AINS_MODE)mode);
    printf("[Bridge] setAINSMode returned: %d\n", ret);
    return ret;
}

// Join a channel (required for AI降噪 to work)
__declspec(dllexport) int __cdecl Bridge_JoinChannel(const char* token, const char* channel, unsigned int uid) {
    if (!g_engine) {
        printf("[Bridge] Engine not initialized\n");
        return -1;
    }

    printf("[Bridge] Joining channel: %s, uid: %u\n", channel ? channel : "(null)", uid);
    int ret = g_engine->joinChannel(token, channel, "", uid);
    printf("[Bridge] joinChannel returned: %d\n", ret);
    return ret;
}

// Leave the current channel
__declspec(dllexport) int __cdecl Bridge_LeaveChannel() {
    if (!g_engine) {
        return -1;
    }
    printf("[Bridge] Leaving channel\n");
    return g_engine->leaveChannel();
}

// Register the C callback for receiving PCM audio frames
__declspec(dllexport) void __cdecl Bridge_RegisterAudioCallback(AudioFrameCallback callback, void* userData) {
    g_audioCallback = callback;
    g_audioCallbackUserData = userData;
    printf("[Bridge] Audio callback registered: %p\n", (void*)callback);
}

// Register the audio frame observer with the media engine
// Call this after Bridge_Init and before Bridge_JoinChannel
__declspec(dllexport) int __cdecl Bridge_RegisterAudioObserver() {
    if (!g_mediaEngine) {
        if (!g_engine) return -1;
        // Retry queryInterface
        int qret = g_engine->queryInterface(AGORA_IID_MEDIA_ENGINE, (void**)&g_mediaEngine);
        if (qret != 0 || !g_mediaEngine) {
            printf("[Bridge] Failed to get media engine: %d\n", qret);
            return qret;
        }
    }

    printf("[Bridge] Registering audio frame observer\n");
    int ret = g_mediaEngine->registerAudioFrameObserver(&g_audioObserver);
    printf("[Bridge] registerAudioFrameObserver returned: %d\n", ret);
    return ret;
}

// Release the engine
__declspec(dllexport) void __cdecl Bridge_Release() {
    printf("[Bridge] Releasing engine\n");
    if (g_mediaEngine) {
        g_mediaEngine->registerAudioFrameObserver(nullptr);
        g_mediaEngine = nullptr;
    }
    if (g_engine) {
        g_engine->release();
        g_engine = nullptr;
    }
    g_audioCallback = nullptr;
    g_audioCallbackUserData = nullptr;
    printf("[Bridge] Released\n");
}

// Get SDK version string
__declspec(dllexport) const char* __cdecl Bridge_GetSdkVersion() {
    static int build = 0;
    return getAgoraSdkVersion(&build);
}

// Enumerate recording devices using Agora SDK's own API
// Returns the number of recording devices
__declspec(dllexport) int __cdecl Bridge_GetRecordingDeviceCount() {
    if (!g_audioDeviceManager) return -1;
    IAudioDeviceCollection* collection = g_audioDeviceManager->enumerateRecordingDevices();
    if (!collection) return -1;
    int count = collection->getCount();
    collection->release();
    return count;
}

// Get recording device name and ID at given index
// Returns 0 on success, -1 on failure
// nameBuf and idBuf are caller-allocated buffers of size bufSize
__declspec(dllexport) int __cdecl Bridge_GetRecordingDeviceInfo(int index, char* nameBuf, int nameBufSize, char* idBuf, int idBufSize) {
    if (!g_audioDeviceManager) return -1;
    IAudioDeviceCollection* collection = g_audioDeviceManager->enumerateRecordingDevices();
    if (!collection) return -1;

    char deviceName[MAX_DEVICE_ID_LENGTH];
    char deviceId[MAX_DEVICE_ID_LENGTH];
    int ret = collection->getDevice(index, deviceName, deviceId);
    collection->release();

    if (ret != 0) return -1;

    strncpy_s(nameBuf, nameBufSize, deviceName, _TRUNCATE);
    strncpy_s(idBuf, idBufSize, deviceId, _TRUNCATE);
    return 0;
}

// Set recording device using Agora SDK's own device ID
__declspec(dllexport) int __cdecl Bridge_SetRecordingDeviceById(const char* deviceId) {
    if (!g_audioDeviceManager) return -1;
    return g_audioDeviceManager->setRecordingDevice(deviceId);
}

// Set whether the SDK follows the system default recording device
// MUST be called AFTER setRecordingDevice to prevent the SDK from
// automatically switching back to the system default
__declspec(dllexport) int __cdecl Bridge_FollowSystemRecordingDevice(bool enable) {
    if (!g_audioDeviceManager) return -1;
    return g_audioDeviceManager->followSystemRecordingDevice(enable);
}

// Get the currently selected recording device ID
// Returns the device ID string in deviceIdBuf, caller allocates buffer
__declspec(dllexport) int __cdecl Bridge_GetRecordingDevice(char* deviceIdBuf, int bufSize) {
    if (!g_audioDeviceManager) return -1;
    char deviceId[MAX_DEVICE_ID_LENGTH] = {0};
    int ret = g_audioDeviceManager->getRecordingDevice(deviceId);
    if (ret == 0) {
        strncpy_s(deviceIdBuf, bufSize, deviceId, _TRUNCATE);
    }
    return ret;
}

} // extern "C"
