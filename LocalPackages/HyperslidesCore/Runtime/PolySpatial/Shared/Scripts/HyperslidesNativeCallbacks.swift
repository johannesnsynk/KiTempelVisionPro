import Foundation
import SwiftUI
import NetworkExtension
import CoreLocation
import CoreImage
import ImageIO
import Metal
import MetalKit
import Darwin

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

//region UserDefaults Access
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

@_cdecl("GetStringFromSharedDefaults")
public func GetStringFromSharedDefaults(key: UnsafePointer<CChar>, group: UnsafePointer<CChar>) -> UnsafeMutablePointer<CChar> {
    let keyString = String(cString: key)
    let groupString = String(cString: group)
    if let value = UserDefaults(suiteName: groupString)?.string(forKey: keyString) {
        return strdup(value)
    }
    return strdup("")
}

@_cdecl("RemoveSharedDefaultsKey")
public func RemoveSharedDefaultsKey(key: UnsafePointer<CChar>, group: UnsafePointer<CChar>) -> Int32 {
    let keyString = String(cString: key)
    let groupString = String(cString: group)

    guard let defaults = UserDefaults(suiteName: groupString) else {
        return 0
    }

    defaults.removeObject(forKey: keyString)
    defaults.synchronize()
    return 1
}
//endregion

//region Shared Data Access
@_cdecl("GetSharedByteArray")
public func GetSharedByteArray(key: UnsafePointer<CChar>, group: UnsafePointer<CChar>, length: UnsafeMutablePointer<Int32>) -> UnsafeMutablePointer<UInt8>? {
    let keyString = String(cString: key)
    let groupString = String(cString: group)
    if let data = UserDefaults(suiteName: groupString)?.data(forKey: keyString) {
        length.pointee = Int32(data.count)
        let buffer = UnsafeMutablePointer<UInt8>.allocate(capacity: data.count)
        data.copyBytes(to: buffer, count: data.count)
        return buffer
    }
    length.pointee = 0
    return nil
}

@_cdecl("GetSharedFileBytes")
public func GetSharedFileBytes(fileName: UnsafePointer<CChar>, group: UnsafePointer<CChar>, length: UnsafeMutablePointer<Int32>) -> UnsafeMutablePointer<UInt8>? {
    let fileNameStr = String(cString: fileName)
    let groupStr = String(cString: group)
    guard let containerURL = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: groupStr) else {
        length.pointee = 0
        return nil
    }
    let fileURL = containerURL.appendingPathComponent(fileNameStr)
    guard let data = try? Data(contentsOf: fileURL) else {
        length.pointee = 0
        return nil
    }
    length.pointee = Int32(data.count)
    let buffer = UnsafeMutablePointer<UInt8>.allocate(capacity: data.count)
    data.copyBytes(to: buffer, count: data.count)
    return buffer
}

@_cdecl("DeleteSharedFileFromAppGroup")
public func DeleteSharedFileFromAppGroup(fileName: UnsafePointer<CChar>, group: UnsafePointer<CChar>) -> Int32 {
    let fileNameStr = String(cString: fileName)
    let groupStr = String(cString: group)

    guard let containerURL = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: groupStr) else {
        return 0
    }

    let fileURL = containerURL.appendingPathComponent(fileNameStr)
    if !FileManager.default.fileExists(atPath: fileURL.path) {
        return 1
    }

    do {
        try FileManager.default.removeItem(at: fileURL)
        return 1
    } catch {
        NSLog("[ShareCleanup] Failed deleting shared file: \(fileURL.lastPathComponent) error: \(error)")
        return 0
    }
}

private func DecodeHDRRGBAHalf(from fileURL: URL,
                               width: UnsafeMutablePointer<Int32>,
                               height: UnsafeMutablePointer<Int32>,
                               length: UnsafeMutablePointer<Int32>) -> UnsafeMutablePointer<UInt8>? {
    var ciImage: CIImage? = CIImage(contentsOf: fileURL)

    if ciImage == nil,
       let source = CGImageSourceCreateWithURL(fileURL as CFURL, nil),
       let cgImage = CGImageSourceCreateImageAtIndex(source, 0, nil) {
        ciImage = CIImage(cgImage: cgImage)
    }

    if ciImage == nil,
       let fileData = try? Data(contentsOf: fileURL) {
        ciImage = CIImage(data: fileData)
    }

    if ciImage == nil,
       let device = MTLCreateSystemDefaultDevice() {
        let loader = MTKTextureLoader(device: device)
        let options: [MTKTextureLoader.Option: Any] = [
            .SRGB: false
        ]

        if let tex = try? loader.newTexture(URL: fileURL, options: options) {
            let w = tex.width
            let h = tex.height
            if w > 0 && h > 0 {
                let region = MTLRegionMake2D(0, 0, w, h)

                if tex.pixelFormat == .rgba16Float {
                    let bytesPerPixel = 8
                    let bytesPerRow = w * bytesPerPixel
                    let dataSize = bytesPerRow * h
                    let out = UnsafeMutablePointer<UInt8>.allocate(capacity: dataSize)
                    tex.getBytes(out, bytesPerRow: bytesPerRow, from: region, mipmapLevel: 0)

                    width.pointee = Int32(w)
                    height.pointee = Int32(h)
                    length.pointee = Int32(dataSize)
                    NSLog("[NativeHDR] Decoded via MetalKit RGBA16F: \(fileURL.lastPathComponent) \(w)x\(h)")
                    return out
                }

                if tex.pixelFormat == .rgba32Float {
                    let srcBytesPerPixel = 16
                    let srcBytesPerRow = w * srcBytesPerPixel
                    let srcSize = srcBytesPerRow * h
                    let src = UnsafeMutablePointer<UInt8>.allocate(capacity: srcSize)
                    tex.getBytes(src, bytesPerRow: srcBytesPerRow, from: region, mipmapLevel: 0)

                    let dstBytesPerPixel = 8
                    let dstBytesPerRow = w * dstBytesPerPixel
                    let dstSize = dstBytesPerRow * h
                    let dst = UnsafeMutablePointer<UInt8>.allocate(capacity: dstSize)

                    let srcFloat = UnsafeRawPointer(src).assumingMemoryBound(to: Float.self)
                    let dstHalf = UnsafeMutableRawPointer(dst).assumingMemoryBound(to: UInt16.self)
                    let count = w * h * 4

                    for i in 0..<count {
                        let half = Float16(srcFloat[i])
                        dstHalf[i] = half.bitPattern
                    }

                    src.deallocate()

                    width.pointee = Int32(w)
                    height.pointee = Int32(h)
                    length.pointee = Int32(dstSize)
                    NSLog("[NativeHDR] Decoded via MetalKit RGBA32F->RGBA16F: \(fileURL.lastPathComponent) \(w)x\(h)")
                    return dst
                }

                NSLog("[NativeHDR] MetalKit texture pixel format unsupported for half-float export: \(tex.pixelFormat.rawValue)")
            }
        }
    }

    guard let resolvedImage = ciImage else {
        width.pointee = 0
        height.pointee = 0
        length.pointee = 0
        NSLog("[NativeHDR] Failed to create CIImage from shared file: \(fileURL.path)")
        return nil
    }

    let extent = resolvedImage.extent.integral
    let w = Int(extent.width)
    let h = Int(extent.height)

    if w <= 0 || h <= 0 {
        width.pointee = 0
        height.pointee = 0
        length.pointee = 0
        NSLog("[NativeHDR] Invalid CIImage extent for file: \(fileURL.lastPathComponent)")
        return nil
    }

    let bytesPerPixel = 8 // RGBA half-float (4 x 16-bit)
    let bytesPerRow = w * bytesPerPixel
    let dataSize = bytesPerRow * h

    let buffer = UnsafeMutablePointer<UInt8>.allocate(capacity: dataSize)

    let context = CIContext(options: [
        .workingColorSpace: NSNull(),
        .outputColorSpace: NSNull()
    ])

    context.render(resolvedImage,
                   toBitmap: buffer,
                   rowBytes: bytesPerRow,
                   bounds: extent,
                   format: .RGBAh,
                   colorSpace: nil)

    width.pointee = Int32(w)
    height.pointee = Int32(h)
    length.pointee = Int32(dataSize)
    NSLog("[NativeHDR] Decoded via CoreImage RGBAh: \(fileURL.lastPathComponent) \(w)x\(h)")
    return buffer
}

@_cdecl("GetSharedHDRRGBAHalf")
public func GetSharedHDRRGBAHalf(fileName: UnsafePointer<CChar>, group: UnsafePointer<CChar>, width: UnsafeMutablePointer<Int32>, height: UnsafeMutablePointer<Int32>, length: UnsafeMutablePointer<Int32>) -> UnsafeMutablePointer<UInt8>? {
    let fileNameStr = String(cString: fileName)
    let groupStr = String(cString: group)
    NSLog("[NativeHDR] Enter GetSharedHDRRGBAHalf for file: \(fileNameStr)")

    guard let containerURL = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: groupStr) else {
        width.pointee = 0
        height.pointee = 0
        length.pointee = 0
        NSLog("[NativeHDR] No container URL for app group: \(groupStr)")
        return nil
    }

    let fileURL = containerURL.appendingPathComponent(fileNameStr)
    return DecodeHDRRGBAHalf(from: fileURL, width: width, height: height, length: length)
}

@_cdecl("GetHDRRGBAHalfFromFilePath")
public func GetHDRRGBAHalfFromFilePath(filePath: UnsafePointer<CChar>, width: UnsafeMutablePointer<Int32>, height: UnsafeMutablePointer<Int32>, length: UnsafeMutablePointer<Int32>) -> UnsafeMutablePointer<UInt8>? {
    let pathString = String(cString: filePath)
    NSLog("[NativeHDR] Enter GetHDRRGBAHalfFromFilePath for path: \(pathString)")

    let fileURL = URL(fileURLWithPath: pathString)
    if !FileManager.default.fileExists(atPath: fileURL.path) {
        width.pointee = 0
        height.pointee = 0
        length.pointee = 0
        NSLog("[NativeHDR] Local file path does not exist: \(fileURL.path)")
        return nil
    }

    return DecodeHDRRGBAHalf(from: fileURL, width: width, height: height, length: length)
}

@_cdecl("FreeSharedByteArray")
public func FreeSharedByteArray(ptr: UnsafeMutablePointer<UInt8>?) {
    ptr?.deallocate()
}

// Return the container path for an app group, or empty string if not accessible
@_cdecl("GetAppGroupContainerPath")
public func GetAppGroupContainerPath(group: UnsafePointer<CChar>) -> UnsafeMutablePointer<CChar> {
    let groupStr = String(cString: group)
    if let url = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: groupStr) {
        return strdup(url.path)
    }
    return strdup("")
}

@_cdecl("FreeSharedCString")
public func FreeSharedCString(ptr: UnsafeMutablePointer<CChar>?) {
    if let ptr {
        free(ptr)
    }
}
//endregion

//region Keychain Access
@_cdecl("SetKeyChainValueFromNative")
public func SetKeyChainValueFromNative(key: UnsafePointer<CChar>, value: UnsafePointer<CChar>, group: UnsafePointer<CChar>) {
    KeychainAccess.SetKeyChainValueFromNative(String(cString: key),
                       value: String(cString: value),
                       accessGroup: String(cString: group))
}

@_cdecl("GetKeyChainValueFromNative")
public func GetKeyChainValueFromNative(key: UnsafePointer<CChar>, group: UnsafePointer<CChar>) -> UnsafeMutablePointer<CChar> {
    if let result = KeychainAccess.GetKeyChainValueFromNative(String(cString: key),
                                       accessGroup: String(cString: group)) {
        return strdup(result)
    }
    return strdup("")
}
//endregion