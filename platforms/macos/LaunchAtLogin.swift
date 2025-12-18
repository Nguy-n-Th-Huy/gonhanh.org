import Foundation
import ServiceManagement

// MARK: - Launch at Login Manager

/// Manages app's launch at login state using SMAppService
/// All SMAppService calls run on background thread to avoid blocking UI
class LaunchAtLoginManager {
    static let shared = LaunchAtLoginManager()
    private init() {}

    /// Check status asynchronously (SMAppService can be slow)
    func checkStatus(completion: @escaping (Bool) -> Void) {
        DispatchQueue.global(qos: .userInitiated).async {
            var enabled = false
            if #available(macOS 13.0, *) {
                enabled = SMAppService.mainApp.status == .enabled
            }
            DispatchQueue.main.async {
                completion(enabled)
            }
        }
    }

    /// Enable launch at login
    func enable(completion: @escaping (Bool) -> Void) {
        DispatchQueue.global(qos: .userInitiated).async {
            var success = false
            if #available(macOS 13.0, *) {
                // Unregister first to prevent duplicate entries (unsigned app issue)
                try? SMAppService.mainApp.unregister()
                do {
                    try SMAppService.mainApp.register()
                    success = SMAppService.mainApp.status == .enabled
                } catch {}
            }
            DispatchQueue.main.async {
                completion(success)
            }
        }
    }

    /// Disable launch at login
    func disable(completion: @escaping (Bool) -> Void) {
        DispatchQueue.global(qos: .userInitiated).async {
            if #available(macOS 13.0, *) {
                try? SMAppService.mainApp.unregister()
            }
            DispatchQueue.main.async {
                completion(true)
            }
        }
    }
}

// MARK: - Mock for Testing

class MockLaunchAtLoginManager {
    var isEnabled: Bool = false

    func checkStatus(completion: @escaping (Bool) -> Void) {
        completion(isEnabled)
    }

    func enable(completion: @escaping (Bool) -> Void) {
        isEnabled = true
        completion(true)
    }

    func disable(completion: @escaping (Bool) -> Void) {
        isEnabled = false
        completion(true)
    }
}
