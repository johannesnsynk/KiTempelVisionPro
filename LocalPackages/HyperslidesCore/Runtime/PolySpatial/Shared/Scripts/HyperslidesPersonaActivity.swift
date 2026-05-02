import Foundation
import GroupActivities
import SwiftUI

struct PersonaGroupActivity: GroupActivity {
    var metadata: GroupActivityMetadata {
        var data = GroupActivityMetadata()
        data.title = String(localized: "Persona Test", comment: "Persona test comment")
        data.subtitle = String(localized: "Persona test subtitle", comment: "Persona Test subtitle comment")
        data.supportsContinuationOnTV = false
        data.type = .generic
        return data
    }
    static var activityIdentifier: String = "com.NSYNK.Hyperslides-Core.persona"
}

@objcMembers
public class GroupActivityManager: NSObject, ObservableObject {
    @Published public var isSessionActive: Bool = false
    private var session: GroupSession<PersonaGroupActivity>?

    public func startGroupActivity() {
        Task {
            do {
                let activity = PersonaGroupActivity()
                try await activity.activate()
                await observeActivity(activity)
            } catch {
                print("Failed to start group activity: \(error.localizedDescription)")
            }
        }
    }

    public func joinGroupActivity() {
        Task {
            do {
                let activity = PersonaGroupActivity()
                try await activity.activate()
                await observeActivity(activity)
            } catch {
                print("Failed to join group activity: \(error.localizedDescription)")
            }
        }
    }
    
    struct PersonaTemplate: SpatialTemplate {
        enum Role: String, SpatialTemplateRole {
            case defaultRole
        }
        
        let elements: [any SpatialTemplateElement] = [
            .seat(position: .app.offsetBy(x: 0.0, z: 0.0), role: Role.defaultRole),
            .seat(position: .app.offsetBy(x: 0.0, z: 0.0), role: Role.defaultRole),
            .seat(position: .app.offsetBy(x: 0.0, z: 0.0), role: Role.defaultRole),
            .seat(position: .app.offsetBy(x: 0.0, z: 0.0), role: Role.defaultRole),
            .seat(position: .app.offsetBy(x: 0.0, z: 0.0), role: Role.defaultRole)
        ]
    }

    private func observeActivity(_ activity: PersonaGroupActivity) async {
        for await session in PersonaGroupActivity.sessions() {
            self.session = session
            
            if let coordinator = await session.systemCoordinator {
                var config = SystemCoordinator.Configuration()
                config.spatialTemplatePreference = .custom(PersonaTemplate())
                config.supportsGroupImmersiveSpace = true
                coordinator.configuration = config
                coordinator.assignRole(PersonaTemplate.Role.defaultRole)
            }
            
            session.join()
            isSessionActive = true
            print("Joined session \(session)")
        }
    }
}



// Example code to be used later
typealias CallbackDelegateType = @convention(c) (UnsafePointer<CChar>) -> Void

var callbackDelegate: CallbackDelegateType? = nil

// Declared in C# as: static extern void SetNativeCallback(CallbackDelegate callback);
@_cdecl("SetNativeCallback")
func setNativeCallback(_ delegate: CallbackDelegateType)
{
    print("############ SET NATIVE CALLBACK")
    callbackDelegate = delegate
}

// This is a function for your own use from the enclosing Unity-VisionOS app, to call the delegate
// from your own windows/views
public func CallCSharpCallback(_ str: String)
{
    if (callbackDelegate == nil) {
        return
    }

    str.withCString {
        callbackDelegate!($0)
    }
}

@_cdecl("StartGroupActivity")
func startGroupActivity(){}

@_cdecl("StopGroupActivity")
func stopGroupActivity(){}

//Example function with string passing in
@_cdecl("StringMethod")
func stringMethod(_ cname: UnsafePointer<CChar>)
{
    let name = String(cString: cname)
    print("############ stringMethod \(name)")
}
