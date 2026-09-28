using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Roost.Core
{
    public static class CredentialStore
    {
        // 早期版本所有厂商共用这一个 key；现在每个厂商各存一份，旧 key 由 MigrateLegacyApiKey 迁移。
        public static string ApiKeyTarget
        {
            get
            {
                string overridden = Environment.GetEnvironmentVariable("ROOST_CREDENTIAL_TARGET");
                return string.IsNullOrEmpty(overridden) ? "Roost/ApiKey" : overridden;
            }
        }

        // 预设按预设 id 区分；自定义服务（以及已不在预设列表里的旧预设）按服务地址的主机名区分。
        public static string ApiKeyTargetFor(string presetId, string baseUrl)
        {
            if (!string.IsNullOrEmpty(presetId) && presetId != AiPresets.CustomId && AiPresets.Find(presetId) != null)
                return ApiKeyTarget + "/" + presetId;
            Uri uri;
            string host = Uri.TryCreate((baseUrl ?? string.Empty).Trim(), UriKind.Absolute, out uri) ? uri.Host.ToLowerInvariant() : null;
            return string.IsNullOrEmpty(host) ? null : ApiKeyTarget + "/custom/" + host;
        }

        public static string ApiKeyTargetFor(RoostSettings settings)
        {
            return ApiKeyTargetFor(settings.AiPresetId, settings.AiBaseUrl);
        }

        public static string ReadApiKey(RoostSettings settings)
        {
            string target = ApiKeyTargetFor(settings);
            return target == null ? null : Read(target);
        }

        // 把旧的共用 key 移到当前所选厂商名下；该厂商已经有 key 时不覆盖。返回是否发生了迁移。
        public static bool MigrateLegacyApiKey(RoostSettings settings)
        {
            string legacy = Read(ApiKeyTarget);
            if (string.IsNullOrEmpty(legacy)) return false;
            string target = settings.AiConfigured ? ApiKeyTargetFor(settings) : null;
            if (target == null) return false;
            if (string.IsNullOrEmpty(Read(target))) Write(target, legacy);
            Delete(ApiKeyTarget);
            return true;
        }
        private const int CRED_TYPE_GENERIC = 1;
        private const int CRED_PERSIST_LOCAL_MACHINE = 2;
        private const int ERROR_NOT_FOUND = 1168;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public int Flags;
            public int Type;
            public string TargetName;
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public int CredentialBlobSize;
            public IntPtr CredentialBlob;
            public int Persist;
            public int AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredWrite(ref CREDENTIAL credential, int flags);

        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredDelete(string target, int type, int flags);

        [DllImport("advapi32.dll")]
        private static extern void CredFree(IntPtr buffer);

        public static void Write(string target, string secret)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(secret ?? string.Empty);
            IntPtr blob = Marshal.AllocHGlobal(Math.Max(1, bytes.Length));
            try
            {
                Marshal.Copy(bytes, 0, blob, bytes.Length);
                CREDENTIAL credential = new CREDENTIAL
                {
                    Type = CRED_TYPE_GENERIC,
                    TargetName = target,
                    CredentialBlobSize = bytes.Length,
                    CredentialBlob = blob,
                    Persist = CRED_PERSIST_LOCAL_MACHINE,
                    UserName = "Roost"
                };
                if (!CredWrite(ref credential, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "无法写入 Windows 凭据管理器。");
            }
            finally
            {
                for (int i = 0; i < bytes.Length; i++) Marshal.WriteByte(blob, i, 0);
                Marshal.FreeHGlobal(blob);
                Array.Clear(bytes, 0, bytes.Length);
            }
        }

        public static string Read(string target)
        {
            IntPtr pointer;
            if (!CredRead(target, CRED_TYPE_GENERIC, 0, out pointer))
            {
                int error = Marshal.GetLastWin32Error();
                if (error == ERROR_NOT_FOUND) return null;
                throw new Win32Exception(error, "无法读取 Windows 凭据管理器。");
            }
            try
            {
                CREDENTIAL credential = (CREDENTIAL)Marshal.PtrToStructure(pointer, typeof(CREDENTIAL));
                if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0) return string.Empty;
                return Marshal.PtrToStringUni(credential.CredentialBlob, credential.CredentialBlobSize / 2);
            }
            finally
            {
                CredFree(pointer);
            }
        }

        public static void Delete(string target)
        {
            if (!CredDelete(target, CRED_TYPE_GENERIC, 0))
            {
                int error = Marshal.GetLastWin32Error();
                if (error != ERROR_NOT_FOUND) throw new Win32Exception(error, "无法删除 Windows 凭据。");
            }
        }
    }
}
