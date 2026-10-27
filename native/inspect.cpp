#include "SpoutDX.h"
#include <vector>
#include <iostream>
#include <thread>
#include <chrono>
int main(int argc, char** argv) {
    spoutDX receiver;
    if (!receiver.OpenDirectX11()) return 1;
    receiver.SetReceiverName(argc > 1 ? argv[1] : "ContentWatch");
    std::vector<unsigned char> pixels(1280*360*4);
    int frames=0; bool alpha=false, opaque=false; unsigned long long hash=0;
    for(int i=0;i<90;i++) {
        auto received = receiver.ReceiveImage(pixels.data(),1280,360);
        receiver.IsUpdated();
        if(received && receiver.IsConnected()) {
            ++frames;
            for(size_t p=3;p<pixels.size();p+=4) { alpha |= pixels[p]==0; opaque |= pixels[p]>0; }
            for(auto b:pixels) hash=hash*31+b;
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(35));
    }
    std::cout << "connectedFrames="<<frames<<" transparent="<<alpha<<" visible="<<opaque<<" hash="<<hash<<"\n";
    return frames>30 && alpha && opaque ? 0 : 2;
}
