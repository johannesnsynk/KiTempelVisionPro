import Foundation
import SwiftUI
import NetworkExtension
import CoreLocation

@_cdecl("GetWifiSSID")
func getWiFiSSID() -> UnsafeMutablePointer<CChar> {
    let locationManager = CLLocationManager()
    locationManager.requestWhenInUseAuthorization()
        
    var ssid: String = "none"
    var isDone = false

    NEHotspotNetwork.fetchCurrent { (network) in
        if let unwrappedNetwork = network {
            ssid = unwrappedNetwork.ssid
        }
        isDone = true
    }

    while !isDone {
        RunLoop.current.run(mode: .default, before: Date(timeIntervalSinceNow: 0.1))
    }

    return strdup(ssid)
}

@_cdecl("GetBoolSetting")
public func GetBoolSetting(key: UnsafePointer<CChar>) -> Bool {
    let keyString = String(cString: key)
    return UserDefaults.standard.bool(forKey: keyString)
}

@_cdecl("GetStringSetting")
public func GetStringSetting(key: UnsafePointer<CChar>) -> UnsafeMutablePointer<CChar> {
    let keyString = String(cString: key)
    if let value = UserDefaults.standard.string(forKey: keyString) {
        return strdup(value) // Convert Swift string to C string
    }
    return strdup("")
}
