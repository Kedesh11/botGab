using System.Security.AccessControl;
using System.Security.Principal;
using MawuGab.Core.Interfaces;

namespace MawuGab.Infrastructure.Security;

public sealed class AclManager : IAclManager
{
    public void EnsureDirectoryAccess(string path)
    {
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(path);

        var dirInfo = new DirectoryInfo(path);
        var security = dirInfo.GetAccessControl();

        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var systemAccount = new NTAccount("SYSTEM");
        security.AddAccessRule(new FileSystemAccessRule(systemAccount, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));

        var serviceSid = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);
        var nsAccount = serviceSid.Translate(typeof(NTAccount)) as NTAccount;
        if (nsAccount != null)
        {
            security.AddAccessRule(new FileSystemAccessRule(nsAccount, FileSystemRights.Modify, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        }

        dirInfo.SetAccessControl(security);
    }
}
