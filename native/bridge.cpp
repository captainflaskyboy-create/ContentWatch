#include "SpoutDX.h"
#include <memory>

extern "C" __declspec(dllexport) void* cw_create(const char* name) noexcept {
    try {
        auto sender = std::make_unique<spoutDX>();
        if (!sender->OpenDirectX11() || !sender->SetSenderName(name)) return nullptr;
        sender->SetSenderFormat(DXGI_FORMAT_B8G8R8A8_UNORM);
        return sender.release();
    } catch (...) { return nullptr; }
}
extern "C" __declspec(dllexport) int cw_send(void* handle, const unsigned char* data, int width, int height, int pitch) noexcept {
    if (!handle || !data || width <= 0 || height <= 0 || pitch < width * 4) return 0;
    try { return static_cast<spoutDX*>(handle)->SendImage(data, width, height, pitch) ? 1 : 0; }
    catch (...) { return 0; }
}
extern "C" __declspec(dllexport) void cw_destroy(void* handle) noexcept {
    if (!handle) return;
    auto sender = static_cast<spoutDX*>(handle);
    sender->ReleaseSender(); sender->CloseDirectX11(); delete sender;
}

// Readback entry points are used by optional hardware integration tests.
extern "C" __declspec(dllexport) void* cw_receiver_create(const char* name) noexcept {
    try {
        auto receiver = std::make_unique<spoutDX>();
        if (!receiver->OpenDirectX11()) return nullptr;
        receiver->SetReceiverName(name);
        return receiver.release();
    } catch (...) { return nullptr; }
}
extern "C" __declspec(dllexport) int cw_receive(void* handle, unsigned char* data, int width, int height) noexcept {
    if (!handle || !data || width <= 0 || height <= 0) return 0;
    try {
        auto receiver = static_cast<spoutDX*>(handle);
        // ReceiveImage reports a new size before copying, so no destination is
        // accessed until its update has been acknowledged and dimensions checked.
        if (receiver->GetSenderWidth() != 0 &&
            (receiver->GetSenderWidth() != width || receiver->GetSenderHeight() != height)) return 0;
        const auto result = receiver->ReceiveImage(data, width, height);
        receiver->IsUpdated();
        return result && receiver->IsConnected() ? 1 : 0;
    } catch (...) { return 0; }
}
extern "C" __declspec(dllexport) void cw_receiver_destroy(void* handle) noexcept {
    if (!handle) return;
    auto receiver = static_cast<spoutDX*>(handle);
    receiver->ReleaseReceiver(); receiver->CloseDirectX11(); delete receiver;
}
