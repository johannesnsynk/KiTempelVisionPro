import Foundation
import AVFoundation
import LiveKit

public typealias LkStringCallback = @convention(c) (UnsafePointer<CChar>?) -> Void

private var room: Room?
private var roomDelegate: BridgeRoomDelegate?
private var logCallback: LkStringCallback?
private var eventCallback: LkStringCallback?

private final class BridgeRoomDelegate: NSObject, RoomDelegate {
    func room(_ room: Room, participantDidConnect participant: RemoteParticipant) {
        if let state = participant.attributes["lk.agent.state"] {
            emitEvent("agent-state:\(state)")
        }
    }

    func room(_ room: Room, participant: Participant, didUpdateAttributes attributes: [String: String]) {
        guard let state = attributes["lk.agent.state"], !state.isEmpty else { return }
        emitEvent("agent-state:\(state)")
    }
}

private func emitLog(_ message: String) {
    guard let callback = logCallback else { return }
    message.withCString { callback($0) }
}

private func emitEvent(_ message: String) {
    guard let callback = eventCallback else { return }
    message.withCString { callback($0) }
}

private func requestMicrophonePermission() async -> Bool {
    if #available(iOS 17.0, visionOS 1.0, *) {
        let status = AVAudioApplication.shared.recordPermission
        switch status {
        case .granted:
            return true
        case .denied:
            return false
        case .undetermined:
            return await AVAudioApplication.requestRecordPermission()
        @unknown default:
            return false
        }
    }

    return await withCheckedContinuation { continuation in
        AVAudioSession.sharedInstance().requestRecordPermission { granted in
            continuation.resume(returning: granted)
        }
    }
}

private func enableMicrophoneWithRetry(roomInstance: Room, attempts: Int = 3) async throws {
    var lastError: Error?

    for attempt in 1...max(1, attempts) {
        do {
            try await roomInstance.localParticipant.setMicrophone(enabled: true)
            emitLog("Microphone enabled (attempt \(attempt))")
            return
        } catch {
            lastError = error
            emitLog("Microphone enable failed (attempt \(attempt)): \(error.localizedDescription)")

            if attempt < attempts {
                // Let the transport/audio graph settle before retrying.
                try? await Task.sleep(nanoseconds: 350_000_000)
            }
        }
    }

    throw lastError ?? NSError(domain: "LiveKitVisionOsBridge", code: -1001, userInfo: [NSLocalizedDescriptionKey: "Unknown microphone error"])
}

@_cdecl("lk_visionos_set_log_callback")
public func lk_visionos_set_log_callback(_ callback: LkStringCallback?) {
    logCallback = callback
}

@_cdecl("lk_visionos_set_event_callback")
public func lk_visionos_set_event_callback(_ callback: LkStringCallback?) {
    eventCallback = callback
}

@_cdecl("lk_visionos_connect")
public func lk_visionos_connect(_ serverUrlPtr: UnsafePointer<CChar>?, _ tokenPtr: UnsafePointer<CChar>?) -> Int32 {
    guard let serverUrlPtr, let tokenPtr else {
        emitEvent("connect-error: missing server URL or token")
        return -1
    }

    let serverUrl = String(cString: serverUrlPtr)
    let token = String(cString: tokenPtr)

    Task {
        do {
            let roomInstance = Room()
            room = roomInstance
            let delegate = BridgeRoomDelegate()
            roomDelegate = delegate
            roomInstance.add(delegate: delegate)

            try await roomInstance.connect(url: serverUrl, token: token)

            emitEvent("connected")
        } catch {
            emitEvent("connect-error: \(error.localizedDescription)")
        }
    }

    emitLog("Started visionOS connect")
    return 0
}

@_cdecl("lk_visionos_disconnect")
public func lk_visionos_disconnect() {
    Task {
        await room?.disconnect()
        roomDelegate = nil
        room = nil
        emitEvent("disconnected")
    }
}

@_cdecl("lk_visionos_set_microphone_enabled")
public func lk_visionos_set_microphone_enabled(_ enabled: Int32) -> Int32 {
    guard let roomInstance = room else {
        emitEvent("microphone-error: room-not-connected")
        return -1
    }

    let shouldEnable = enabled != 0

    Task {
        do {
            if shouldEnable {
                let permissionGranted = await requestMicrophonePermission()
                if !permissionGranted {
                    emitEvent("microphone-error: permission-denied")
                    return
                }

                // A short delay makes microphone startup less flaky after signaling becomes active.
                try? await Task.sleep(nanoseconds: 250_000_000)
                try await enableMicrophoneWithRetry(roomInstance: roomInstance)
                emitEvent("microphone-enabled")
                return
            }

            try await roomInstance.localParticipant.setMicrophone(enabled: false)
            emitEvent("microphone-disabled")
        } catch {
            emitEvent("microphone-error: \(error.localizedDescription)")
        }
    }

    return 0
}
