param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,

    [Parameter(Mandatory = $true)]
    [string]$IconPath,

    [int]$RetryCount = 5
)

$ErrorActionPreference = 'Stop'

$source = @'
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

public static class IconResourceUpdater
{
    private static readonly IntPtr RT_ICON = new IntPtr(3);
    private static readonly IntPtr RT_GROUP_ICON = new IntPtr(14);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr BeginUpdateResource(string pFileName, bool bDeleteExistingResources);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateResource(IntPtr hUpdate, IntPtr lpType, IntPtr lpName, ushort wLanguage, byte[] lpData, int cbData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool EndUpdateResource(IntPtr hUpdate, bool fDiscard);

    public static void SetIcon(string exePath, string icoPath)
    {
        var ico = File.ReadAllBytes(icoPath);
        if (BitConverter.ToUInt16(ico, 0) != 0 || BitConverter.ToUInt16(ico, 2) != 1)
        {
            throw new InvalidDataException("The icon file is not a valid .ico file.");
        }

        var count = BitConverter.ToUInt16(ico, 4);
        if (count == 0)
        {
            throw new InvalidDataException("The icon file does not contain images.");
        }

        var hUpdate = BeginUpdateResource(exePath, false);
        if (hUpdate == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            var group = new MemoryStream();
            var writer = new BinaryWriter(group);
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write(count);

            for (var i = 0; i < count; i++)
            {
                var entryOffset = 6 + (16 * i);
                var bytesInRes = BitConverter.ToUInt32(ico, entryOffset + 8);
                var imageOffset = BitConverter.ToUInt32(ico, entryOffset + 12);
                var image = new byte[bytesInRes];
                Buffer.BlockCopy(ico, (int)imageOffset, image, 0, (int)bytesInRes);

                var resourceId = (ushort)(i + 1);
                if (!UpdateResource(hUpdate, RT_ICON, new IntPtr(resourceId), 0, image, image.Length))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                writer.Write(ico[entryOffset]);
                writer.Write(ico[entryOffset + 1]);
                writer.Write(ico[entryOffset + 2]);
                writer.Write(ico[entryOffset + 3]);
                writer.Write(BitConverter.ToUInt16(ico, entryOffset + 4));
                writer.Write(BitConverter.ToUInt16(ico, entryOffset + 6));
                writer.Write(bytesInRes);
                writer.Write(resourceId);
            }

            writer.Flush();
            var groupData = group.ToArray();
            writer.Dispose();
            group.Dispose();
            foreach (var groupId in new[] { 1, 32512 })
            {
                if (!UpdateResource(hUpdate, RT_GROUP_ICON, new IntPtr(groupId), 0, groupData, groupData.Length))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }
        catch
        {
            EndUpdateResource(hUpdate, true);
            throw;
        }

        if (!EndUpdateResource(hUpdate, false))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}
'@

if (-not ([System.Management.Automation.PSTypeName]'IconResourceUpdater').Type) {
    Add-Type -TypeDefinition $source
}

$resolvedExe = [System.IO.Path]::GetFullPath($ExePath)
$resolvedIcon = [System.IO.Path]::GetFullPath($IconPath)

$attempt = 0
while ($true) {
    try {
        [IconResourceUpdater]::SetIcon($resolvedExe, $resolvedIcon)
        break
    }
    catch {
        $attempt++
        if ($attempt -ge $RetryCount) {
            throw
        }

        Start-Sleep -Milliseconds (300 * $attempt)
    }
}

Write-Host "Updated icon: $resolvedExe"
