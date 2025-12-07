import Cocoa
import SwiftUI

// MARK: - Menu Bar Controller

class MenuBarController {
    private var statusItem: NSStatusItem!
    private var onboardingWindow: NSWindow?
    private var aboutWindow: NSWindow?

    private var isEnabled = true
    private var currentMethod: InputMode = .telex

    init() {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)

        NotificationCenter.default.addObserver(
            self,
            selector: #selector(onboardingDidComplete),
            name: .onboardingCompleted,
            object: nil
        )

        setupMenu()
        updateStatusButton()

        if UserDefaults.standard.bool(forKey: SettingsKey.hasCompletedOnboarding) && AXIsProcessTrusted() {
            loadSettings()
            startEngine()
        } else {
            showOnboarding()
        }
    }

    // MARK: - Setup

    private func loadSettings() {
        isEnabled = UserDefaults.standard.object(forKey: SettingsKey.enabled) as? Bool ?? true
        currentMethod = InputMode(rawValue: UserDefaults.standard.integer(forKey: SettingsKey.method)) ?? .telex
    }

    private func startEngine() {
        RustBridge.initialize()
        KeyboardHookManager.shared.start()
        RustBridge.setEnabled(isEnabled)
        RustBridge.setMethod(currentMethod.rawValue)
    }

    @objc private func onboardingDidComplete() {
        loadSettings()
        updateStatusButton()
        updateMenu()
        startEngine()
    }

    // MARK: - Status Button

    private func updateStatusButton() {
        guard let button = statusItem.button else { return }
        button.title = isEnabled ? "V" : "E"
        button.font = .systemFont(ofSize: 14, weight: .semibold)
        button.contentTintColor = isEnabled ? .controlAccentColor : .secondaryLabelColor
    }

    // MARK: - Menu

    private func setupMenu() {
        let menu = NSMenu()

        // Header
        let header = NSMenuItem()
        header.view = createHeaderView()
        menu.addItem(header)
        menu.addItem(.separator())

        // Toggle
        let toggle = NSMenuItem(title: "Tắt gõ tiếng Việt", action: #selector(toggleEnabled), keyEquivalent: " ")
        toggle.target = self
        toggle.tag = 1
        menu.addItem(toggle)
        menu.addItem(.separator())

        // Methods
        let telex = NSMenuItem(title: "Telex", action: #selector(selectTelex), keyEquivalent: "1")
        telex.keyEquivalentModifierMask = .command
        telex.target = self
        telex.tag = 10
        menu.addItem(telex)

        let vni = NSMenuItem(title: "VNI", action: #selector(selectVNI), keyEquivalent: "2")
        vni.keyEquivalentModifierMask = .command
        vni.target = self
        vni.tag = 11
        menu.addItem(vni)
        menu.addItem(.separator())

        // About & Help
        let about = NSMenuItem(title: "Giới thiệu \(AppMetadata.name)", action: #selector(showAbout), keyEquivalent: "")
        about.target = self
        menu.addItem(about)

        let help = NSMenuItem(title: "Góp ý & Báo lỗi", action: #selector(openHelp), keyEquivalent: "")
        help.target = self
        menu.addItem(help)
        menu.addItem(.separator())

        // Quit
        let quit = NSMenuItem(title: "Thoát", action: #selector(NSApp.terminate), keyEquivalent: "q")
        menu.addItem(quit)

        statusItem.menu = menu
        updateMenu()
    }

    private func createHeaderView() -> NSView {
        let view = NSView(frame: NSRect(x: 0, y: 0, width: 200, height: 44))

        let name = NSTextField(labelWithString: AppMetadata.name)
        name.font = .systemFont(ofSize: 13, weight: .bold)
        name.frame = NSRect(x: 14, y: 24, width: 100, height: 16)
        view.addSubview(name)

        let dot = NSView(frame: NSRect(x: 14, y: 8, width: 8, height: 8))
        dot.wantsLayer = true
        dot.layer?.cornerRadius = 4
        dot.layer?.backgroundColor = (isEnabled ? NSColor.systemGreen : .systemGray).cgColor
        view.addSubview(dot)

        let status = NSTextField(labelWithString: isEnabled ? "Đang bật • \(currentMethod.name)" : "Đang tắt")
        status.font = .systemFont(ofSize: 11)
        status.textColor = .secondaryLabelColor
        status.frame = NSRect(x: 26, y: 4, width: 100, height: 16)
        view.addSubview(status)

        let version = NSTextField(labelWithString: "v\(AppMetadata.version)")
        version.font = .systemFont(ofSize: 10)
        version.textColor = .tertiaryLabelColor
        version.alignment = .right
        version.frame = NSRect(x: 140, y: 24, width: 46, height: 14)
        view.addSubview(version)

        return view
    }

    private func updateMenu() {
        guard let menu = statusItem.menu else { return }
        menu.items.first?.view = createHeaderView()
        menu.item(withTag: 1)?.title = isEnabled ? "Tắt gõ tiếng Việt" : "Bật gõ tiếng Việt"
        menu.item(withTag: 10)?.state = currentMethod == .telex ? .on : .off
        menu.item(withTag: 11)?.state = currentMethod == .vni ? .on : .off
    }

    // MARK: - Actions

    @objc private func toggleEnabled() {
        isEnabled.toggle()
        UserDefaults.standard.set(isEnabled, forKey: SettingsKey.enabled)
        RustBridge.setEnabled(isEnabled)
        updateStatusButton()
        updateMenu()
    }

    @objc private func selectTelex() { setMethod(.telex) }
    @objc private func selectVNI() { setMethod(.vni) }

    private func setMethod(_ mode: InputMode) {
        currentMethod = mode
        UserDefaults.standard.set(mode.rawValue, forKey: SettingsKey.method)
        RustBridge.setMethod(mode.rawValue)
        updateStatusButton()
        updateMenu()
    }

    private func showOnboarding() {
        if onboardingWindow == nil {
            let view = OnboardingView()
            let controller = NSHostingController(rootView: view)
            onboardingWindow = NSWindow(contentViewController: controller)
            onboardingWindow?.title = AppMetadata.name
            onboardingWindow?.styleMask = [.titled, .closable]
            onboardingWindow?.setContentSize(controller.view.fittingSize)
            onboardingWindow?.center()
        }
        onboardingWindow?.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    @objc private func showAbout() {
        if aboutWindow == nil {
            let controller = NSHostingController(rootView: AboutView())
            let window = NSWindow(contentViewController: controller)
            window.title = "Giới thiệu \(AppMetadata.name)"
            window.styleMask = [.titled, .closable]
            window.setContentSize(NSSize(width: 300, height: 340))
            window.center()
            window.isReleasedWhenClosed = false
            aboutWindow = window
        }
        NSApp.activate(ignoringOtherApps: true)
        aboutWindow?.makeKeyAndOrderFront(nil)
    }

    @objc private func openHelp() {
        NSWorkspace.shared.open(URL(string: AppMetadata.issuesURL)!)
    }
}
