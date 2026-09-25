#import <AVFoundation/AVFoundation.h>
#import <Foundation/Foundation.h>
#include <atomic>

// Runs inside Unity/the signed player: TCC permission belongs to that app, not a helper process.
// Never opens an AVCaptureSession and never alters the TCC database.
static std::atomic_bool pending(false);
extern "C" {
__attribute__((visibility("default"))) int FAA_CameraAuthorizationState() {
    @autoreleasepool {
        return (int)[AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeVideo];
    }
}
__attribute__((visibility("default"))) int FAA_CameraUsageDeclared() {
    @autoreleasepool {
        id value = [[NSBundle mainBundle] objectForInfoDictionaryKey:@"NSCameraUsageDescription"];
        return [value isKindOfClass:[NSString class]] && [(NSString *)value length] > 0 ? 1 : 0;
    }
}
__attribute__((visibility("default"))) int FAA_CameraRequestPending() { return pending.load() ? 1 : 0; }
__attribute__((visibility("default"))) int FAA_RequestCameraAuthorization() {
    @autoreleasepool {
        if (FAA_CameraUsageDeclared() != 1) return -2;
        if (FAA_CameraAuthorizationState() != AVAuthorizationStatusNotDetermined) return 0;
        if (pending.exchange(true)) return 1;
        dispatch_async(dispatch_get_main_queue(), ^{
            [AVCaptureDevice requestAccessForMediaType:AVMediaTypeVideo completionHandler:^(BOOL granted) {
                pending.store(false);
            }];
        });
        return 1;
    }
}
}
