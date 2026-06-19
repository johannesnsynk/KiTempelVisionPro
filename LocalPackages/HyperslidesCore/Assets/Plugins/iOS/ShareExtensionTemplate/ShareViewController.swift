import UIKit
import os

class ShareViewController: UIViewController {
    private var baseBundleIdentifier: String {
        let bundleIdentifier = Bundle.main.bundleIdentifier ?? "com.nsynk.hyperslides"
        if bundleIdentifier.hasSuffix(".share") {
            return String(bundleIdentifier.dropLast(".share".count))
        }
        return bundleIdentifier
    }

    private var appGroup: String {
        return "group.\(baseBundleIdentifier)"
    }

    private var appURLScheme: String {
        return baseBundleIdentifier
            .split(separator: ".")
            .last
            .map { String($0).lowercased() }
            ?? "hyperslides"
    }

    private let shareLog = OSLog(subsystem: Bundle.main.bundleIdentifier ?? "ShareExtension", category: "ShareExtension")

    private var savedFileName: String?
    private var savedFileURL: URL?
    private var infoLabel: UILabel?

    override func viewDidLoad() {
        super.viewDidLoad()
        view.backgroundColor = UIColor.systemBackground

        // Minimal UI placeholder while processing attachments
        setupPlaceholderUI()

        guard let items = extensionContext?.inputItems as? [NSExtensionItem] else {
            finishExtension()
            return
        }

        for item in items {
            guard let attachments = item.attachments else { continue }
            for provider in attachments {
                // Determine the best type identifier to use - prefer specific formats to avoid conversion
                let typeIdentifier = bestTypeIdentifier(for: provider)
                
                if let typeIdentifier = typeIdentifier {
                    // Use loadInPlaceFileRepresentation to preserve original file format and metadata
                    provider.loadInPlaceFileRepresentation(forTypeIdentifier: typeIdentifier) { url, isInPlace, error in
                        if let url = url {
                            // use the original filename if available
                            let receivedName = url.lastPathComponent
                            os_log("ShareExtension: loaded file %{public}@ (inPlace: %{public}@, type: %{public}@)",
                                   type: .info, receivedName, String(isInPlace), typeIdentifier)
                            // copy then present UI to the user
                            self.copyToAppGroup(fileURL: url, fileName: receivedName)
                            DispatchQueue.main.async {
                                self.showUIForSharedFile(name: receivedName)
                            }
                        } else {
                            os_log("ShareExtension: loadInPlaceFileRepresentation failed: %{public}@",
                                   type: .error, String(describing: error))
                            DispatchQueue.main.async {
                                self.infoLabel?.text = "Failed to load shared item."
                                // auto-close shortly
                                DispatchQueue.main.asyncAfter(deadline: .now() + 1.0) { self.finishExtension() }
                            }
                        }
                    }
                    return
                }
            }
        }

        // nothing to share
        finishExtension()
    }

    func containerURL() -> URL? {
        return FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: appGroup)
    }

    /// Returns the most specific type identifier for the provider to avoid format conversion.
    /// Prioritizes EXR and HDR formats to preserve metadata.
    private func bestTypeIdentifier(for provider: NSItemProvider) -> String? {
        // EXR or HDR format - OpenEXR high dynamic range image
        if provider.hasItemConformingToTypeIdentifier("com.ilm.openexr-image") {
            return "com.ilm.openexr-image"
        }
        if provider.hasItemConformingToTypeIdentifier("public.radiance") {
            return "public.radiance"
        }
        // This helps preserve original format instead of converting
        for typeId in provider.registeredTypeIdentifiers {
            // Prefer specific image formats over generic "public.image"
            if typeId != "public.image" && typeId != "public.data" {
                return typeId
            }
        }
        // Fallback to generic image
        if provider.hasItemConformingToTypeIdentifier("public.image") {
            return "public.image"
        }
        return nil
    }

    func copyToAppGroup(fileURL: URL, fileName: String) {
        guard let container = containerURL() else {
            os_log("ShareExtension: container not available for group: %{public}@",type: .error, appGroup)
            return
        }
        let dest = container.appendingPathComponent(fileName)
        try? FileManager.default.removeItem(at: dest)
        do {
            try FileManager.default.copyItem(at: fileURL, to: dest)
            UserDefaults(suiteName: appGroup)?.set(Date().timeIntervalSince1970, forKey: "last_shared_image")
            UserDefaults(suiteName: appGroup)?.set(fileName, forKey: "last_shared_file_name")
            UserDefaults(suiteName: appGroup)?.synchronize()
            os_log("ShareExtension: wrote shared file to app group container: %{public}@",type: .info, dest.path)
            if let containerPath = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: appGroup)?.path {
                os_log("ShareExtension: container path available: %{public}@",type: .info, containerPath)
            } else {
                os_log("ShareExtension: container path lookup failed for group: %{public}@",type: .error, appGroup)
            }
            savedFileName = fileName
            savedFileURL = dest
        } catch {
            os_log("ShareExtension: copy error: %{public}@",type: .error, String(describing: error))
        }
    }

    // MARK: - UI
    private func setupPlaceholderUI() {
        let label = UILabel()
        label.translatesAutoresizingMaskIntoConstraints = false
        label.text = "Preparing..."
        label.textAlignment = .center
        view.addSubview(label)
        NSLayoutConstraint.activate([
            label.centerXAnchor.constraint(equalTo: view.centerXAnchor),
            label.centerYAnchor.constraint(equalTo: view.centerYAnchor, constant: -20),
            label.leadingAnchor.constraint(greaterThanOrEqualTo: view.leadingAnchor, constant: 12),
            label.trailingAnchor.constraint(lessThanOrEqualTo: view.trailingAnchor, constant: -12)
        ])
        infoLabel = label
    }

    private func showUIForSharedFile(name: String) {
        infoLabel?.text = "Received: \(name)"
        preferredContentSize = CGSize(width: 320, height: 140)

        // Open in App button
        let openBtn = UIButton(type: .system)
        openBtn.translatesAutoresizingMaskIntoConstraints = false
        openBtn.setTitle("Open in App", for: .normal)
        openBtn.addTarget(self, action: #selector(openInAppTapped), for: .touchUpInside)
        view.addSubview(openBtn)

        // Done button
        let doneBtn = UIButton(type: .system)
        doneBtn.translatesAutoresizingMaskIntoConstraints = false
        doneBtn.setTitle("Done", for: .normal)
        doneBtn.addTarget(self, action: #selector(doneTapped), for: .touchUpInside)
        view.addSubview(doneBtn)

        NSLayoutConstraint.activate([
            openBtn.centerXAnchor.constraint(equalTo: view.centerXAnchor),
            openBtn.topAnchor.constraint(equalTo: infoLabel!.bottomAnchor, constant: 12),
            doneBtn.centerXAnchor.constraint(equalTo: view.centerXAnchor),
            doneBtn.topAnchor.constraint(equalTo: openBtn.bottomAnchor, constant: 8)
        ])
    }

    @objc private func openInAppTapped() {
        let raw = savedFileName ?? ""
        var components = URLComponents()
        components.scheme = appURLScheme
        components.host = "share"
        components.queryItems = [
            URLQueryItem(name: "file", value: raw),
            URLQueryItem(name: "t", value: String(Int(Date().timeIntervalSince1970)))
        ]

        os_log("ShareExtension: open in app tapped with url: %{public}@",type: .info, components.url?.absoluteString ?? "nil")

        if let url = components.url {
            extensionContext?.open(url, completionHandler: { success in
                os_log("ShareExtension: open URL result: %{public}@ for file: %{public}@",type: .info, String(success), raw)
                if success {
                    self.finishExtension()
                } else {
                    let fallbackSuccess = self.tryOpenViaUIApplication(url)
                    os_log("ShareExtension: UIApplication fallback result: %{public}@", type: .info, String(fallbackSuccess))
                    self.finishExtension()
                }
            })
        } else {
            os_log("ShareExtension: failed to construct URL for file: %{public}@",type: .error, raw)
            finishExtension()
        }
    }

    private func tryOpenViaUIApplication(_ url: URL) -> Bool {
#if !os(visionOS)
        var responder: UIResponder? = self
        while let current = responder {
            if let application = current as? UIApplication {
                if #available(iOS 18.0, *) {
                    application.open(url, options: [:], completionHandler: nil)
                    return true
                } else {
                    return application.perform(#selector(UIApplication.openURL(_:)), with: url) != nil
                }
            }
            responder = current.next
        }
        return false
#else
        // Not available on visionOS
        return false
#endif
    }

    @objc private func doneTapped() {
        finishExtension()
    }

    private func finishExtension() {
        extensionContext?.completeRequest(returningItems: [], completionHandler: nil)
    }
}
