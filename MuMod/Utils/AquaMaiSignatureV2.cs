using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace MuMod.Utils;

public static class AquaMaiSignatureV2
{
    #region Unity Mono ECDSA Polyfill
    
    private const string BCRYPT = "bcrypt.dll";
    private const string BCRYPT_ECDSA_P521_ALGORITHM = "ECDSA_P521";
    private const string BCRYPT_ECCPUBLIC_BLOB = "ECCPUBLICBLOB";

    [DllImport(BCRYPT)]
    private static extern int BCryptOpenAlgorithmProvider(
        out IntPtr phAlgorithm,
        [MarshalAs(UnmanagedType.LPWStr)] string pszAlgId,
        [MarshalAs(UnmanagedType.LPWStr)] string pszImplementation,
        uint dwFlags);

    [DllImport(BCRYPT)]
    private static extern int BCryptImportKeyPair(
        IntPtr hAlgorithm,
        IntPtr hImportKey,
        [MarshalAs(UnmanagedType.LPWStr)] string pszBlobType,
        out IntPtr phKey,
        byte[] pbInput,
        int cbInput,
        uint dwFlags);

    [DllImport(BCRYPT)]
    private static extern int BCryptVerifySignature(
        IntPtr hKey,
        IntPtr pPaddingInfo,
        byte[] pbHash,
        int cbHash,
        byte[] pbSignature,
        int cbSignature,
        uint dwFlags);

    [DllImport(BCRYPT)]
    private static extern int BCryptDestroyKey(IntPtr hKey);

    [DllImport(BCRYPT)]
    private static extern int BCryptCloseAlgorithmProvider(IntPtr hAlgorithm, uint dwFlags);

    // BCRYPT_ECCKEY_BLOB 结构
    [StructLayout(LayoutKind.Sequential)]
    private struct BCRYPT_ECCKEY_BLOB
    {
        public uint dwMagic;  // BCRYPT_ECDSA_PUBLIC_P521_MAGIC = 0x35534345
        public uint cbKey;    // 66 for P-521
    }
    
    private const uint BCRYPT_ECDSA_PUBLIC_P521_MAGIC = 0x35534345; // "ECS5"

    
    private static byte[] ExtractEccPointFromSpki(byte[] spki)
    {
        const int pointOffset = 24;
        const int pointLength = 133;
        
        if (spki.Length < pointOffset + 1 + pointLength)
            throw new ArgumentException("Invalid SPKI length");
        
        var point = new byte[pointLength];
        Array.Copy(spki, pointOffset + 1, point, 0, pointLength);
        
        if (point[0] != 0x04)
            throw new ArgumentException("Expected uncompressed point (0x04)");
        
        var xy = new byte[132];
        Array.Copy(point, 1, xy, 0, 132);
        return xy;
    }

    private static byte[] BuildCngPublicKeyBlob(byte[] xy)
    {
        var blob = new byte[8 + 132];
        
        blob[0] = 0x45;
        blob[1] = 0x43; 
        blob[2] = 0x53; 
        blob[3] = 0x35; 
        blob[4] = 0x42;
        blob[5] = 0x00;
        blob[6] = 0x00;
        blob[7] = 0x00;
        
        Array.Copy(xy, 0, blob, 8, 132);
        return blob;
    }

    private static bool VerifyWithCng(byte[] publicKeySpki, byte[] hash, byte[] signature)
    {
        var xy = ExtractEccPointFromSpki(publicKeySpki);
        var keyBlob = BuildCngPublicKeyBlob(xy);
        
        IntPtr hAlg = IntPtr.Zero;
        IntPtr hKey = IntPtr.Zero;
        
        try
        {
            int status = BCryptOpenAlgorithmProvider(out hAlg, BCRYPT_ECDSA_P521_ALGORITHM, null, 0);
            if (status != 0)
                throw new Exception($"BCryptOpenAlgorithmProvider failed: 0x{status:X8}");

            status = BCryptImportKeyPair(hAlg, IntPtr.Zero, BCRYPT_ECCPUBLIC_BLOB, out hKey, keyBlob, keyBlob.Length, 0);
            if (status != 0)
                throw new Exception($"BCryptImportKeyPair failed: 0x{status:X8}");

            status = BCryptVerifySignature(hKey, IntPtr.Zero, hash, hash.Length, signature, signature.Length, 0);
            return status == 0; // STATUS_SUCCESS
        }
        finally
        {
            if (hKey != IntPtr.Zero) BCryptDestroyKey(hKey);
            if (hAlg != IntPtr.Zero) BCryptCloseAlgorithmProvider(hAlg, 0);
        }
    }

    
    #endregion


    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct AquaMaiSignatureBlock
    {
        public PubKeyId KeyId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 132)]
        public byte[] Signature;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public byte[] Magic;
        public byte Version;
    }

    private static AquaMaiSignatureBlock? parseFromBytes(byte[] data)
    {
        var size = Marshal.SizeOf<AquaMaiSignatureBlock>();
        if (data.Length < size)
        {
            return null;
        }

        var block = new byte[size];
        Array.Copy(data, data.Length - size, block, 0, size);
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.Copy(block, 0, ptr, size);
            var stru = Marshal.PtrToStructure<AquaMaiSignatureBlock>(ptr);
            if (stru.Magic == null || !stru.Magic.SequenceEqual(System.Text.Encoding.UTF8.GetBytes("AquaMaiSig")))
            {
                return null;
            }
            if (stru.Version != 1)
            {
                return null;
            }
            return stru;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    public enum PubKeyId : byte
    {
        None,
        Local,
        CI,
    }

    private static readonly Dictionary<PubKeyId, byte[]> pubKeys = new()
    {
        {
            PubKeyId.Local,
            Convert.FromBase64String(
                "MIGbMBAGByqGSM49AgEGBSuBBAAjA4GGAAQBVoScU915wnWeDOvLsQd3uWh9NwclPhup8TT+cqiV3SB683JgQTpLYv2XRCGfH/3zIwTU2KDIXwNPsDPlOpH0QIkB0aVIDo9g6mus7cTMphq/7yjQQEPnsBQO5KbtcNxcy7mSnhykSea2Gv+iOKu1C4FOaO39zNe0HULVoqMrcCNLRkg=")
        },
        {
            PubKeyId.CI,
            Convert.FromBase64String(
                "MIGbMBAGByqGSM49AgEGBSuBBAAjA4GGAAQAvi9gtqbPF0g7K52lumBRiztMb5lVKbTwhzwVSVsMBUo5wXp9w86CnIh3/VErXtyneP1BBMLFDEtd4Cb11eQmxBMBuPjY61oca4gZhIxgQ8e0ki/pUhtUQwIQ48AN/gba/lq0GWBaPrwEyhSvHArHsPo2WxFczdsOO0mTgwq0bAw/tTw=")
        },
    };

    public enum VerifyStatus
    {
        NotFound,
        InvalidKeyId,
        InvalidSignature,
        Valid,
    }

    public record VerifyResult(VerifyStatus Status, PubKeyId KeyId);

    public static VerifyResult VerifySignature(byte[] data)
    {
        var block = parseFromBytes(data);
        if (block == null)
        {
            return new VerifyResult(VerifyStatus.NotFound, PubKeyId.None);
        }

        if (!pubKeys.TryGetValue(block.Value.KeyId, out var pubKey))
        {
            return new VerifyResult(VerifyStatus.InvalidKeyId, block.Value.KeyId);
        }

        var size = Marshal.SizeOf<AquaMaiSignatureBlock>();
        byte[] hash;
        using (var sha256 = SHA256.Create())
        {
            hash = sha256.ComputeHash(data, 0, data.Length - size);
        }
        var isValid = VerifyWithCng(pubKey, hash, block.Value.Signature);
        return new VerifyResult(isValid ? VerifyStatus.Valid : VerifyStatus.InvalidSignature, block.Value.KeyId);
    }
}