#include "SpoutDX.h"
#include <vector>
#include <iostream>
#include <thread>
#include <chrono>
extern "C" __declspec(dllimport) void* cw_create(const char*);
extern "C" __declspec(dllimport) int cw_send(void*, const unsigned char*, int, int, int);
extern "C" __declspec(dllimport) void cw_destroy(void*);

int main() {
    const int w = 64, h = 32;
    const char* name = "ContentWatch.NativeSmoke";
    auto sender = cw_create(name);
    if (!sender) { std::cerr << "FAIL: sender creation\n"; return 1; }
    spoutDX receiver;
    if (!receiver.OpenDirectX11()) { cw_destroy(sender); return 2; }
    receiver.SetReceiverName(name);
    std::vector<unsigned char> source(w*h*4), received(w*h*4);
    int verified = 0;
    for (int frame = 0; frame < 30; ++frame) {
        for (int y=0; y<h; ++y) for(int x=0; x<w; ++x) {
            auto i=(y*w+x)*4;
            source[i]=static_cast<unsigned char>(frame); // B advances every frame
            source[i+1]=static_cast<unsigned char>(y);   // vertical orientation
            source[i+2]=static_cast<unsigned char>(x);   // R channel
            source[i+3]=128;                            // premultiplied half alpha
        }
        if (!cw_send(sender, source.data(), w, h, w*4)) { std::cerr<<"FAIL: send\n"; return 3; }
        receiver.ReceiveImage(received.data(), w, h);
        receiver.IsUpdated(); // acknowledge initial dimensions so ReceiveImage can start copying
        bool exact = frame > 2;
        const auto receivedFrame = received[0]; // SDK pipelines readback by one frame
        exact &= receivedFrame >= frame - 2 && receivedFrame <= frame;
        for(int y=0;y<h;y++) for(int x=0;x<w;x++) {
            auto i=(y*w+x)*4;
            exact &= received[i]==receivedFrame && received[i+1]==y && received[i+2]==x && received[i+3]==128;
        }
        if (receiver.IsConnected() && exact) ++verified;
        std::this_thread::sleep_for(std::chrono::milliseconds(35));
    }
    receiver.ReleaseReceiver(); receiver.CloseDirectX11(); cw_destroy(sender);
    std::cout << "Exact BGRA/alpha/orientation frames: " << verified << "/30\n";
    return verified >= 20 ? 0 : 4;
}
