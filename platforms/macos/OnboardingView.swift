import SwiftUI
import AppKit

// MARK: - Onboarding View

struct OnboardingView: View {
    @State private var currentPage = 0
    @State private var hasPermission = false
    @State private var selectedMode: InputMode = .telex
    @State private var permissionTimer: Timer?

    var body: some View {
        VStack(spacing: 0) {
            // Content area
            Group {
                switch currentPage {
                case 0:
                    WelcomePage()
                case 1:
                    PermissionPage(
                        hasPermission: hasPermission,
                        onOpenSettings: openAccessibilitySettings
                    )
                case 2:
                    SetupPage(selectedMode: $selectedMode)
                default:
                    EmptyView()
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)

            Divider()

            // Bottom bar
            HStack {
                // Page indicators
                HStack(spacing: 8) {
                    ForEach(0..<3, id: \.self) { index in
                        Circle()
                            .fill(index == currentPage ? Color.accentColor : Color.secondary.opacity(0.3))
                            .frame(width: 6, height: 6)
                    }
                }

                Spacer()

                // Buttons
                HStack(spacing: 12) {
                    if currentPage > 0 {
                        Button("Quay lại") {
                            currentPage -= 1
                        }
                    }

                    primaryButton
                }
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 16)
        }
        .frame(width: 480)
        .fixedSize(horizontal: false, vertical: true)
        .onAppear { startPermissionCheck() }
        .onDisappear { stopPermissionCheck() }
    }

    @ViewBuilder
    private var primaryButton: some View {
        switch currentPage {
        case 0:
            Button("Tiếp tục") {
                currentPage = hasPermission ? 2 : 1
            }
            .keyboardShortcut(.defaultAction)
            .buttonStyle(.borderedProminent)

        case 1:
            Button("Khởi động lại") {
                restartApp()
            }
            .keyboardShortcut(.defaultAction)
            .buttonStyle(.borderedProminent)
            .disabled(!hasPermission)

        case 2:
            Button("Hoàn tất") {
                finishOnboarding()
            }
            .keyboardShortcut(.defaultAction)
            .buttonStyle(.borderedProminent)

        default:
            EmptyView()
        }
    }

    // MARK: - Actions

    private func openAccessibilitySettings() {
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") {
            NSWorkspace.shared.open(url)
        }
    }

    private func restartApp() {
        UserDefaults.standard.set(selectedMode.rawValue, forKey: SettingsKey.method)

        let path = Bundle.main.bundlePath
        let task = Process()
        task.launchPath = "/bin/sh"
        task.arguments = ["-c", "sleep 0.5 && open \"\(path)\""]
        try? task.run()
        NSApp.terminate(nil)
    }

    private func finishOnboarding() {
        UserDefaults.standard.set(selectedMode.rawValue, forKey: SettingsKey.method)
        UserDefaults.standard.set(true, forKey: SettingsKey.hasCompletedOnboarding)
        NotificationCenter.default.post(name: .onboardingCompleted, object: nil)
        NSApp.keyWindow?.close()
    }

    // MARK: - Permission Timer

    private func startPermissionCheck() {
        checkPermission()
        permissionTimer = Timer.scheduledTimer(withTimeInterval: 1.0, repeats: true) { _ in
            checkPermission()
        }
    }

    private func stopPermissionCheck() {
        permissionTimer?.invalidate()
        permissionTimer = nil
    }

    private func checkPermission() {
        hasPermission = AXIsProcessTrusted()
    }
}

// MARK: - Welcome Page

private struct WelcomePage: View {
    var body: some View {
        VStack(spacing: 20) {
            Spacer()

            Image(nsImage: NSApp.applicationIconImage)
                .resizable()
                .frame(width: 100, height: 100)

            Text("Chào mừng đến với \(AppMetadata.name)")
                .font(.system(size: 24, weight: .bold))

            Text(AppMetadata.tagline)
                .font(.body)
                .foregroundStyle(.secondary)

            Spacer()
        }
        .padding(40)
    }
}

// MARK: - Permission Page

private struct PermissionPage: View {
    let hasPermission: Bool
    let onOpenSettings: () -> Void

    var body: some View {
        VStack(spacing: 20) {
            Spacer()

            Image(systemName: "hand.raised.fill")
                .font(.system(size: 48))
                .foregroundStyle(.blue)

            Text("Cần quyền Accessibility")
                .font(.system(size: 24, weight: .bold))

            Text("\(AppMetadata.name) cần quyền Accessibility để gõ tiếng Việt.")
                .font(.body)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
                .frame(maxWidth: 340)

            // Steps
            VStack(alignment: .leading, spacing: 12) {
                PermissionStep(number: 1, text: "Mở System Settings → Privacy & Security → Accessibility", isComplete: false)
                PermissionStep(number: 2, text: "Bật \(AppMetadata.name) trong danh sách", isComplete: hasPermission)
                PermissionStep(number: 3, text: "Nhấn \"Khởi động lại\" để áp dụng", isComplete: false)
            }
            .padding(.top, 8)

            Button(action: onOpenSettings) {
                Label("Mở System Settings", systemImage: "gear")
            }
            .buttonStyle(.link)

            Spacer()
        }
        .padding(32)
    }
}

private struct PermissionStep: View {
    let number: Int
    let text: String
    let isComplete: Bool

    var body: some View {
        HStack(spacing: 12) {
            ZStack {
                Circle()
                    .fill(isComplete ? Color.green : Color.secondary.opacity(0.2))
                    .frame(width: 24, height: 24)

                if isComplete {
                    Image(systemName: "checkmark")
                        .font(.system(size: 12, weight: .bold))
                        .foregroundStyle(.white)
                } else {
                    Text("\(number)")
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundStyle(.secondary)
                }
            }

            Text(text)
                .font(.callout)
                .foregroundStyle(isComplete ? .secondary : .primary)
        }
    }
}

// MARK: - Setup Page

private struct SetupPage: View {
    @Binding var selectedMode: InputMode

    var body: some View {
        VStack(spacing: 20) {
            Spacer()

            Image(systemName: "keyboard")
                .font(.system(size: 48))
                .foregroundStyle(.blue)

            Text("Chọn kiểu gõ")
                .font(.system(size: 24, weight: .bold))

            Text("Bạn có thể thay đổi trong menu bất cứ lúc nào.")
                .font(.body)
                .foregroundStyle(.secondary)

            VStack(spacing: 8) {
                ForEach(InputMode.allCases, id: \.rawValue) { mode in
                    ModeOption(
                        mode: mode,
                        isSelected: selectedMode == mode,
                        onSelect: { selectedMode = mode }
                    )
                }
            }
            .frame(maxWidth: 280)
            .padding(.top, 8)

            Spacer()
        }
        .padding(32)
    }
}

private struct ModeOption: View {
    let mode: InputMode
    let isSelected: Bool
    let onSelect: () -> Void

    var body: some View {
        Button(action: onSelect) {
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text(mode.name)
                        .font(.headline)
                    Text(mode.description)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                Spacer()

                Image(systemName: isSelected ? "checkmark.circle.fill" : "circle")
                    .font(.title2)
                    .foregroundStyle(isSelected ? .blue : .secondary.opacity(0.4))
            }
            .padding(12)
            .background(
                RoundedRectangle(cornerRadius: 8)
                    .fill(isSelected ? Color.blue.opacity(0.1) : Color.secondary.opacity(0.05))
            )
            .overlay(
                RoundedRectangle(cornerRadius: 8)
                    .stroke(isSelected ? Color.blue.opacity(0.5) : Color.clear, lineWidth: 1)
            )
        }
        .buttonStyle(.plain)
    }
}

// MARK: - Notification

extension Notification.Name {
    static let onboardingCompleted = Notification.Name("onboardingCompleted")
}
