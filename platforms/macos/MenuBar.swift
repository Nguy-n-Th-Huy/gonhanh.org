import Cocoa
import SwiftUI

// MARK: - Menu Bar Controller (Apple HIG Compliant)

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

        let hasOnboarded = UserDefaults.standard.bool(forKey: SettingsKey.hasCompletedOnboarding)
        let hasPermission = AXIsProcessTrusted()

        if hasOnboarded && hasPermission {
            loadSettings()
            setupUI()
            startEngine()
        } else {
            setupUI()
            showOnboarding()
        }
    }

    // MARK: - Setup

    private func loadSettings() {
        let defaults = UserDefaults.standard

        isEnabled = defaults.object(forKey: SettingsKey.enabled) != nil
            ? defaults.bool(forKey: SettingsKey.enabled)
            : true

        let methodValue = defaults.integer(forKey: SettingsKey.method)
        currentMethod = InputMode(rawValue: methodValue) ?? .telex
    }

    private func setupUI() {
        updateStatusButton()
        setupMenu()
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

        if isEnabled {
            button.image = NSImage(systemSymbolName: "keyboard.fill", accessibilityDescription: AppMetadata.name)
            button.title = " \(currentMethod.shortName)"
        } else {
            button.image = NSImage(systemSymbolName: "keyboard", accessibilityDescription: AppMetadata.name)
            button.title = ""
        }
        button.imagePosition = .imageLeading
    }

    // MARK: - Menu

    private func setupMenu() {
        let menu = NSMenu()

        // Header
        let headerItem = NSMenuItem()
        headerItem.view = createHeaderView()
        menu.addItem(headerItem)

        menu.addItem(.separator())

        // Enable toggle
        let enableItem = NSMenuItem(
            title: "Enable \(AppMetadata.name)",
            action: #selector(toggleEnabled),
            keyEquivalent: ""
        )
        enableItem.target = self
        enableItem.state = isEnabled ? .on : .off
        enableItem.tag = 100
        menu.addItem(enableItem)

        menu.addItem(.separator())

        // Input method section
        let methodLabel = NSMenuItem(title: "Input Method", action: nil, keyEquivalent: "")
        methodLabel.isEnabled = false
        menu.addItem(methodLabel)

        let telexItem = NSMenuItem(title: "Telex", action: #selector(selectTelex), keyEquivalent: "t")
        telexItem.keyEquivalentModifierMask = [.command, .shift]
        telexItem.target = self
        telexItem.tag = 200
        telexItem.state = currentMethod == .telex ? .on : .off
        telexItem.indentationLevel = 1
        menu.addItem(telexItem)

        let vniItem = NSMenuItem(title: "VNI", action: #selector(selectVNI), keyEquivalent: "v")
        vniItem.keyEquivalentModifierMask = [.command, .shift]
        vniItem.target = self
        vniItem.tag = 201
        vniItem.state = currentMethod == .vni ? .on : .off
        vniItem.indentationLevel = 1
        menu.addItem(vniItem)

        menu.addItem(.separator())

        // About
        let aboutItem = NSMenuItem(
            title: "About \(AppMetadata.name)",
            action: #selector(showAbout),
            keyEquivalent: ""
        )
        aboutItem.target = self
        menu.addItem(aboutItem)

        // Help
        let helpItem = NSMenuItem(
            title: "Help & Feedback",
            action: #selector(openHelp),
            keyEquivalent: "?"
        )
        helpItem.target = self
        menu.addItem(helpItem)

        menu.addItem(.separator())

        // Quit
        let quitItem = NSMenuItem(
            title: "Quit \(AppMetadata.name)",
            action: #selector(quit),
            keyEquivalent: "q"
        )
        quitItem.target = self
        menu.addItem(quitItem)

        statusItem.menu = menu
    }

    private func createHeaderView() -> NSView {
        let view = NSView(frame: NSRect(x: 0, y: 0, width: 200, height: 40))

        let title = NSTextField(labelWithString: AppMetadata.name)
        title.font = .systemFont(ofSize: 13, weight: .semibold)
        title.frame = NSRect(x: 14, y: 20, width: 120, height: 16)
        view.addSubview(title)

        let status = NSTextField(labelWithString: isEnabled ? "On - \(currentMethod.name)" : "Off")
        status.font = .systemFont(ofSize: 11)
        status.textColor = isEnabled ? .systemGreen : .secondaryLabelColor
        status.frame = NSRect(x: 14, y: 4, width: 100, height: 14)
        view.addSubview(status)

        let version = NSTextField(labelWithString: "v\(AppMetadata.version)")
        version.font = .systemFont(ofSize: 10)
        version.textColor = .tertiaryLabelColor
        version.alignment = .right
        version.frame = NSRect(x: 140, y: 20, width: 46, height: 14)
        view.addSubview(version)

        return view
    }

    private func updateMenu() {
        guard let menu = statusItem.menu else { return }

        // Update header
        if let headerItem = menu.items.first {
            headerItem.view = createHeaderView()
        }

        // Update states
        menu.item(withTag: 100)?.state = isEnabled ? .on : .off
        menu.item(withTag: 200)?.state = currentMethod == .telex ? .on : .off
        menu.item(withTag: 201)?.state = currentMethod == .vni ? .on : .off
    }

    // MARK: - Actions

    @objc private func toggleEnabled() {
        isEnabled.toggle()
        UserDefaults.standard.set(isEnabled, forKey: SettingsKey.enabled)
        RustBridge.setEnabled(isEnabled)
        updateStatusButton()
        updateMenu()
    }

    @objc private func selectTelex() {
        setMethod(.telex)
    }

    @objc private func selectVNI() {
        setMethod(.vni)
    }

    private func setMethod(_ mode: InputMode) {
        currentMethod = mode
        UserDefaults.standard.set(mode.rawValue, forKey: SettingsKey.method)
        RustBridge.setMethod(mode.rawValue)
        updateStatusButton()
        updateMenu()
    }

    @objc private func showOnboarding() {
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
            let view = AboutView()
            let controller = NSHostingController(rootView: view)
            aboutWindow = NSWindow(contentViewController: controller)
            aboutWindow?.title = "About \(AppMetadata.name)"
            aboutWindow?.styleMask = [.titled, .closable]
            aboutWindow?.setContentSize(NSSize(width: 300, height: 340))
            aboutWindow?.center()
        }
        aboutWindow?.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    @objc private func openHelp() {
        if let url = URL(string: AppMetadata.issuesURL) {
            NSWorkspace.shared.open(url)
        }
    }

    @objc private func quit() {
        NSApp.terminate(nil)
    }
}
