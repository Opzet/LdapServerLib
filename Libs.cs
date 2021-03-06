using Sys = global::System;
using SysConv = global::System.Convert;
using SysTxt = global::System.Text;
using SysCll = global::System.Collections;
using SysClG = global::System.Collections.Generic;
using SysSock = global::System.Net.Sockets;
using SysCfg = global::System.Configuration;
using SysService = global::System.ServiceProcess;
using LDap = global::Libs.LDAP;
using LCore = global::Libs.LDAP.Core;
using LServ = global::Libs.Service;
namespace Libs.Service
{
    public interface IServer : Sys.IDisposable
    {
        bool IsStarted { get; }
        void Start();
        void Stop();
    }

    public sealed class ServiceRunner : SysService.ServiceBase
    {
        private SysClG.IList<LServ.IServer> Servers;
        public void Execute() { if (this.Servers != null) { foreach (LServ.IServer Server in this.Servers) { Server.Start(); } } }

        protected override void OnStart(string[] args)
        {
            this.Execute();
            base.OnStart(args);
        }

        protected override void OnStop()
        {
            if (this.Servers != null) { foreach (LServ.IServer Server in this.Servers) { Server.Stop(); } }
            base.OnStop();
        }

        protected override void OnPause()
        {
            if (this.Servers != null) { foreach (LServ.IServer Server in this.Servers) { Server.Stop(); } }
            base.OnPause();
        }

        protected override void OnContinue()
        {
            this.OnStart(null);
            base.OnContinue();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (this.Servers != null)
            {
                foreach (LServ.IServer Server in this.Servers) { Server.Dispose(); }
                this.Servers.Clear();
                this.Servers = null;
            }
        }

        public ServiceRunner(SysClG.IList<LServ.IServer> Servers) : base() { this.Servers = Servers; }
    }

    [Sys.ComponentModel.RunInstaller(true)] public abstract class ServiceInstaller : SysCfg.Install.Installer
    {
        internal const string LogDateFormat = "yyyy-MM-dd hh:mm:ss.fff";
        public const string InstallKey = "install";
        public const string StartKey = "start";
        public const string AutoStartKey = "auto";
        public const string PauseKey = "pause";
        public const string ContinueKey = "continue";
        public const string StopKey = "stop";
        public const string UninstallKey = "uninstall";
        public const string RunKey = "run";
        private SysService.ServiceProcessInstaller pInstaller;
        private SysService.ServiceInstaller sInstaller;
        protected static bool ServiceIsRunning(string ServiceName) { using (SysService.ServiceController sc = new SysService.ServiceController(ServiceName)) { try { return (sc.Status == SysService.ServiceControllerStatus.Running); } catch { return false; } } }
        protected static void ServicePause(string ServiceName) { using (SysService.ServiceController sc = new SysService.ServiceController(ServiceName)) { if (sc.Status == SysService.ServiceControllerStatus.Running) { sc.Pause(); } } }
        protected static void ServiceContinue(string ServiceName) { using (SysService.ServiceController sc = new SysService.ServiceController(ServiceName)) { if (sc.Status == SysService.ServiceControllerStatus.Paused) { sc.Continue(); } } }
        protected static void ServiceStart(string ServiceName) { using (SysService.ServiceController sc = new SysService.ServiceController(ServiceName)) { if (sc.Status != SysService.ServiceControllerStatus.Running) { sc.Start(); } } }
        protected static void ServiceStop(string ServiceName) { using (SysService.ServiceController sc = new SysService.ServiceController(ServiceName)) { if (sc.Status == SysService.ServiceControllerStatus.Running) { sc.Stop(); } } }
        protected static void ServiceInstall(string ServiceName) { SysCfg.Install.ManagedInstallerClass.InstallHelper(new string[] { LServ.ServiceInstaller.Location(true) }); }
        protected static void ServiceUninstall(string ServiceName) { SysCfg.Install.ManagedInstallerClass.InstallHelper(new string[] { "/u", LServ.ServiceInstaller.Location(true) }); }

        public static string Location(bool FileName) //APPDATA for Services is on (something like): C:\Windows\System32\config\systemprofile\AppData\Roaming
        {
            Sys.Reflection.Assembly Asm = Sys.Reflection.Assembly.GetExecutingAssembly();
            string File = string.Empty;
            if (FileName) { File = Asm.CodeBase.Replace("file:///", string.Empty).Replace('/', '\\'); } else { File = Asm.CodeBase.Replace("file:///", string.Empty).Replace('/', '\\').Replace(Asm.ManifestModule.Name, string.Empty); }
            return File;
        }

        private static void Run(SysClG.IList<LServ.IServer> Servers)
        {
            Sys.Console.WriteLine("Will Instantiate Server with " + Servers.Count.ToString() + " Servers Listening (" + Sys.DateTime.Now.ToString(LServ.ServiceInstaller.LogDateFormat, Sys.Globalization.CultureInfo.InvariantCulture) + ")");
            using (LServ.ServiceRunner instance = new LServ.ServiceRunner(Servers))
            {
                Sys.Console.WriteLine("Server Instantiated, Will call the \"Execute()\" method (" + Sys.DateTime.Now.ToString(LServ.ServiceInstaller.LogDateFormat, Sys.Globalization.CultureInfo.InvariantCulture) + ")");
                instance.Execute();
                Sys.Console.WriteLine("\"Execute()\" method completed, all Servers are Listening (" + Sys.DateTime.Now.ToString(LServ.ServiceInstaller.LogDateFormat, Sys.Globalization.CultureInfo.InvariantCulture) + ")");
                Sys.Console.WriteLine("Server is Running, press any key to Stop it (or \"c\" to clear log)");
                Sys.Console.WriteLine();
                Sys.ConsoleKeyInfo K = Sys.Console.ReadKey();
                while (K.KeyChar == 'c' || K.KeyChar == 'C')
                {
                    Sys.Console.Clear();
                    K = Sys.Console.ReadKey();
                }
            }
            Sys.Console.WriteLine();
            Sys.Console.WriteLine("Server has Stoped");
            Sys.Console.WriteLine("Press any ket to exit");
            Sys.Console.ReadKey();
        }

        protected static int Process(string ServiceName, SysClG.IList<LServ.IServer> Servers, params string[] args)
        {
            if (args == null || args.Length == 0)
            {
#if DEBUG
                LServ.ServiceInstaller.Run(Servers);
#else
                if (!LServ.ServiceInstaller.ServiceIsRunning(ServiceName)) { SysService.ServiceBase.Run(new LServ.ServiceRunner(Servers)); }
#endif
            }
            else
            {
                switch (args[0].ToLower())
                {
                    case LServ.ServiceInstaller.InstallKey:
                        LServ.ServiceInstaller.ServiceInstall(ServiceName);
                        if (args.Length == 2 && args[1] == LServ.ServiceInstaller.AutoStartKey) { LServ.ServiceInstaller.ServiceStart(ServiceName); }
                        break;
                    case LServ.ServiceInstaller.UninstallKey:
                        LServ.ServiceInstaller.ServiceStop(ServiceName);
                        LServ.ServiceInstaller.ServiceUninstall(ServiceName);
                        break;
                    case LServ.ServiceInstaller.StopKey: LServ.ServiceInstaller.ServiceStop(ServiceName); break;
                    case LServ.ServiceInstaller.ContinueKey: LServ.ServiceInstaller.ServiceContinue(ServiceName); break;
                    case LServ.ServiceInstaller.PauseKey: LServ.ServiceInstaller.ServicePause(ServiceName); break;
                    case LServ.ServiceInstaller.StartKey: LServ.ServiceInstaller.ServiceStart(ServiceName); break;
                    case LServ.ServiceInstaller.RunKey:
                    default: LServ.ServiceInstaller.Run(Servers); break;
                }
            }
            return 0;
        }

        protected ServiceInstaller(string ServiceName, string ServiceDescription = "") : base()
        {
            this.Context = new SysCfg.Install.InstallContext((LServ.ServiceInstaller.Location(false) + "server.log"), new string[] { /* NOTHING */ });
            this.pInstaller = new SysService.ServiceProcessInstaller();
            this.pInstaller.Account = SysService.ServiceAccount.LocalSystem;
            this.pInstaller.Context = this.Context;
            this.Installers.Add(this.pInstaller);
            this.sInstaller = new SysService.ServiceInstaller();
            this.sInstaller.ServiceName = ServiceName;
            this.sInstaller.DisplayName = ServiceName;
            this.sInstaller.Description = (string.IsNullOrEmpty(ServiceDescription) ? ServiceName : ServiceDescription);
            this.sInstaller.StartType = SysService.ServiceStartMode.Automatic;
            this.sInstaller.Context = this.Context;
            this.Installers.Add(this.sInstaller); //knowledge base http://www.bryancook.net/2008/04/running-multiple-net-services-within.html
        }
    }
}
namespace Libs.LDAP
{
    namespace Core //https://github.com/vforteli/Flexinets.Ldap.Core | https://tools.ietf.org/html/rfc4511
    {
        internal delegate bool Verify<T>(T obj);

        internal static class Utils
        {
            internal static byte[] StringToByteArray(string hex, bool trimWhitespace = true)
            {
                if (trimWhitespace) { hex = hex.Replace(" ", string.Empty); }
                int NumberChars = hex.Length;
                byte[] bytes = new byte[NumberChars / 2];
                for (int i = 0; i < NumberChars; i += 2) { bytes[i / 2] = SysConv.ToByte(hex.Substring(i, 2), 16); }
                return bytes;
            }

            internal static string ByteArrayToString(byte[] bytes)
            {
                SysTxt.StringBuilder hex = new SysTxt.StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes) { hex.Append(b.ToString("X2")); }
                return hex.ToString();
            }

            internal static string BitsToString(SysCll.BitArray bits)
            {
                int i = 1;
                string derp = string.Empty;
                foreach (object bit in bits)
                {
                    derp += SysConv.ToInt32(bit);
                    if (i % 8 == 0) { derp += " "; }
                    i++;
                }
                return derp.Trim();
            }

            internal static byte[] IntToBerLength(int length) //https://en.wikipedia.org/wiki/X.690#BER_encoding
            {
                if (length <= 127) { return new byte[] { (byte)length }; }
                else
                {
                    byte[] intbytes = Sys.BitConverter.GetBytes(length);
                    Sys.Array.Reverse(intbytes);
                    byte intbyteslength = (byte)intbytes.Length;
                    int lengthByte = intbyteslength + 128;
                    byte[] berBytes = new byte[1 + intbyteslength];
                    berBytes[0] = (byte)lengthByte;
                    Sys.Buffer.BlockCopy(intbytes, 0, berBytes, 1, intbyteslength);
                    return berBytes;
                }
            }

            internal static TObject[] Reverse<TObject>(SysClG.IEnumerable<TObject> enumerable)
            {
                SysClG.List<TObject> acum = new SysClG.List<TObject>(10);
                foreach (TObject obj in enumerable)
                {
                    if (acum.Count == acum.Capacity) { acum.Capacity += 10; }
                    acum.Add(obj);
                }
                acum.Reverse();
                return acum.ToArray();
            }

            internal static bool Any<T>(SysClG.IEnumerable<T> enumerator, LCore.Verify<T> verifier) { foreach (T obj in enumerator) { if (verifier(obj)) { return true; } } return false; }
            internal static T SingleOrDefault<T>(SysClG.IEnumerable<T> enumerator, LCore.Verify<T> verifier) { foreach (T obj in enumerator) { if (verifier(obj)) { return obj; } } return default(T); }

            private sealed class ArraySegmentEnumerator<T> : SysClG.IEnumerator<T>, SysClG.IEnumerable<T> //https://referencesource.microsoft.com/#mscorlib/system/arraysegment.cs,9b6becbc5eb6a533
            {
                private T[] _array;
                private int _start;
                private int _end;
                private int _current;

                public bool MoveNext()
                {
                    if (this._current < this._end)
                    {
                        this._current++;
                        return (this._current < this._end);
                    }
                    return false;
                }

                public T Current { get { if (this._current < this._start) throw new Sys.InvalidOperationException(); else if (this._current >= this._end) throw new Sys.InvalidOperationException(); else return this._array[this._current]; } }
                SysClG.IEnumerator<T> SysClG.IEnumerable<T>.GetEnumerator() { return this; }
                SysCll.IEnumerator SysCll.IEnumerable.GetEnumerator() { return this; }
                object SysCll.IEnumerator.Current { get { return this.Current; } }
                void SysCll.IEnumerator.Reset() { this._current = this._start - 1; }
                void Sys.IDisposable.Dispose() { /* NOTHING */ }

                internal ArraySegmentEnumerator(T[] Array, int Start, int Count)
                {
                    this._array = Array;
                    this._start = Start;
                    this._end = this._start + Count;
                    this._current = this._start - 1;
                }
            }

            internal static int BerLengthToInt(byte[] bytes, int offset, out int berByteCount)
            {
                berByteCount = 1;
                int attributeLength = 0;
                if (bytes[offset] >> 7 == 1)
                {
                    int lengthoflengthbytes = bytes[offset] & 127;
                    byte[] temp = LCore.Utils.Reverse<byte>(new LCore.Utils.ArraySegmentEnumerator<byte>(bytes, offset + 1, lengthoflengthbytes));
                    Sys.Array.Resize<byte>(ref temp, 4);
                    attributeLength = Sys.BitConverter.ToInt32(temp, 0);
                    berByteCount += lengthoflengthbytes;
                }
                else { attributeLength = bytes[offset] & 127; }
                return attributeLength;
            }

            internal static int BerLengthToInt(Sys.IO.Stream stream, out int berByteCount)
            {
                berByteCount = 1;
                int attributeLength = 0;
                byte[] berByte = new byte[1];
                stream.Read(berByte, 0, 1);
                if (berByte[0] >> 7 == 1)
                {
                    int lengthoflengthbytes = berByte[0] & 127;
                    byte[] lengthBytes = new byte[lengthoflengthbytes];
                    stream.Read(lengthBytes, 0, lengthoflengthbytes);
                    byte[] temp = LCore.Utils.Reverse<byte>(lengthBytes);
                    Sys.Array.Resize<byte>(ref temp, 4);
                    attributeLength = Sys.BitConverter.ToInt32(temp, 0);
                    berByteCount += lengthoflengthbytes;
                }
                else { attributeLength = berByte[0] & 127; }
                return attributeLength;
            }

            internal static string Repeat(string stuff, int n)
            {
                SysTxt.StringBuilder concat = new SysTxt.StringBuilder(stuff.Length * n);
                for (int i = 0; i < n; i++) { concat.Append(stuff); }
                return concat.ToString();
            }
        }

        public enum LdapFilterChoice : byte
        {
            and = 0,
            or = 1,
            not = 2,
            equalityMatch = 3,
            substrings = 4,
            greaterOrEqual = 5,
            lessOrEqual = 6,
            present = 7,
            approxMatch = 8,
            extensibleMatch = 9
        }

        public enum LdapOperation : byte
        {
            BindRequest = 0,
            BindResponse = 1,
            UnbindRequest = 2,
            SearchRequest = 3,
            SearchResultEntry = 4,
            SearchResultDone = 5,
            SearchResultReference = 19,
            ModifyRequest = 6,
            ModifyResponse = 7,
            AddRequest = 8,
            AddResponse = 9,
            DelRequest = 10,
            DelResponse = 11,
            ModifyDNRequest = 12,
            ModifyDNResponse = 13,
            CompareRequest = 14,
            CompareResponse = 15,
            AbandonRequest = 16,
            ExtendedRequest = 23,
            ExtendedResponse = 24,
            IntermediateResponse = 25,
            NONE = 255 //SAMMUEL (not in protocol - never use it!)
        }

        public enum LdapResult : byte
        {
            success = 0,
            operationError = 1,
            protocolError = 2,
            timeLimitExceeded = 3,
            sizeLimitExceeded = 4,
            compareFalse = 5,
            compareTrue = 6,
            authMethodNotSupported = 7,
            strongerAuthRequired = 8, // 9 reserved --
            referral = 10,
            adminLimitExceeded = 11,
            unavailableCriticalExtension = 12,
            confidentialityRequired = 13,
            saslBindInProgress = 14,
            noSuchAttribute = 16,
            undefinedAttributeType = 17,
            inappropriateMatching = 18,
            constraintViolation = 19,
            attributeOrValueExists = 20,
            invalidAttributeSyntax = 21, // 22-31 unused --
            noSuchObject = 32,
            aliasProblem = 33,
            invalidDNSyntax = 34, // 35 reserved for undefined isLeaf --
            aliasDereferencingProblem = 36, // 37-47 unused --
            inappropriateAuthentication = 48,
            invalidCredentials = 49,
            insufficientAccessRights = 50,
            busy = 51,
            unavailable = 52,
            unwillingToPerform = 53,
            loopDetect = 54, // 55-63 unused --
            namingViolation = 64,
            objectClassViolation = 65,
            notAllowedOnNonLeaf = 66,
            notAllowedOnRDN = 67,
            entryAlreadyExists = 68,
            objectClassModsProhibited = 69, // 70 reserved for CLDAP --
            affectsMultipleDSAs = 71, // 72-79 unused --
            other = 80
        }

        public enum TagClass : byte
        {
            Universal = 0,
            Application = 1,
            Context = 2,
            Private = 3
        }

        public enum UniversalDataType : byte
        {
            EndOfContent = 0,
            Boolean = 1,
            Integer = 2,
            BitString = 3,
            OctetString = 4,
            Null = 5,
            ObjectIdentifier = 6,
            ObjectDescriptor = 7,
            External = 8,
            Real = 9,
            Enumerated = 10,
            EmbeddedPDV = 11,
            UTF8String = 12,
            Relative = 13,
            Reserved = 14,
            Reserved2 = 15,
            Sequence = 16,
            Set = 17,
            NumericString = 18,
            PrintableString = 19,
            T61String = 20,
            VideotexString = 21,
            IA5String = 22,
            UTCTime = 23,
            GeneralizedTime = 24,
            GraphicString = 25,
            VisibleString = 26,
            GeneralString = 27,
            UniversalString = 28,
            CharacterString = 29,
            BMPString = 30,
            NONE = 255 //SAMMUEL (not in protocol - never use it!)
        }

        internal enum Scope : byte
        {
            baseObject = 0,
            singleLevel = 1,
            wholeSubtree = 2
        }

        public class Tag
        {
            public byte TagByte { get; internal set; }
            public LCore.TagClass Class { get { return (LCore.TagClass)(this.TagByte >> 6); } }
            public LCore.UniversalDataType DataType { get { return this.Class == LCore.TagClass.Universal ? (LCore.UniversalDataType)(this.TagByte & 31) : LCore.UniversalDataType.NONE; } }
            public LCore.LdapOperation LdapOperation { get { return this.Class == LCore.TagClass.Application ? (LCore.LdapOperation)(this.TagByte & 31) : LCore.LdapOperation.NONE; } }
            public byte? ContextType { get { return this.Class == LCore.TagClass.Context ? (byte?)(this.TagByte & 31) : null; } }
            public static LCore.Tag Parse(byte tagByte) { return new LCore.Tag { TagByte = tagByte }; }
            public override string ToString() { return "Tag[class=" + this.Class.ToString() + ",datatype=" + this.DataType.ToString() + ",ldapoperation=" + this.LdapOperation.ToString() + ",contexttype=" + (this.ContextType == null ? "NULL" : ((LCore.LdapFilterChoice)this.ContextType).ToString()) + "]"; }

            public bool IsConstructed
            {
                get { return new SysCll.BitArray(new byte[] { this.TagByte }).Get(5); }
                set
                {
                    SysCll.BitArray foo = new SysCll.BitArray(new byte[] { this.TagByte });
                    foo.Set(5, value);
                    byte[] temp = new byte[1];
                    foo.CopyTo(temp, 0);
                    this.TagByte = temp[0];
                }
            }

            private Tag() { /* NOTHING */ }
            public Tag(LCore.LdapOperation operation) { TagByte = (byte)((byte)operation + ((byte)LCore.TagClass.Application << 6)); }
            public Tag(LCore.UniversalDataType dataType) { TagByte = (byte)(dataType + ((byte)LCore.TagClass.Universal << 6)); }
            public Tag(byte context) { TagByte = (byte)(context + ((byte)LCore.TagClass.Context << 6)); }
        }

        public class LdapAttribute : Sys.IDisposable
        {
            private LCore.Tag _tag;
            internal byte[] Value = new byte[0];
            public SysClG.List<LCore.LdapAttribute> ChildAttributes = new SysClG.List<LCore.LdapAttribute>();
            public LCore.TagClass Class { get { return this._tag.Class; } }
            public bool IsConstructed { get { return (this._tag.IsConstructed || this.ChildAttributes.Count > 0); } }
            public LCore.LdapOperation LdapOperation { get { return this._tag.LdapOperation; } }
            public LCore.UniversalDataType DataType { get { return this._tag.DataType; } }
            public byte? ContextType { get { return this._tag.ContextType; } }

            public object GetValue()
            {
                if (this._tag.Class == LCore.TagClass.Universal)
                {
                    switch (this._tag.DataType)
                    {
                        case LCore.UniversalDataType.Boolean: return Sys.BitConverter.ToBoolean(this.Value, 0);
                        case LCore.UniversalDataType.Integer:
                            byte[] intbytes = new byte[4];
                            Sys.Buffer.BlockCopy(this.Value, 0, intbytes, 4 - this.Value.Length, this.Value.Length);
                            Sys.Array.Reverse(intbytes);
                            return Sys.BitConverter.ToInt32(intbytes, 0);
                        default: return SysTxt.Encoding.UTF8.GetString(this.Value, 0, this.Value.Length);
                    }
                }
                return SysTxt.Encoding.UTF8.GetString(Value, 0, Value.Length);
            }

            private byte[] GetBytes(object val)
            {
                if (val == null) { return new byte[0]; }
                else
                {
                    Sys.Type typeOFval = val.GetType();
                    if (typeOFval == typeof(string)) { return SysTxt.Encoding.UTF8.GetBytes(val as string); }
                    else if (typeOFval == typeof(int)) { return LCore.Utils.Reverse<byte>(Sys.BitConverter.GetBytes((int)val)); }
                    else if (typeOFval == typeof(byte)) { return new byte[] { (byte)val }; }
                    else if (typeOFval == typeof(bool)) { return Sys.BitConverter.GetBytes((bool)val); }
                    else if (typeOFval == typeof(byte[])) { return (val as byte[]); }
                    else { throw new Sys.InvalidOperationException("Nothing found for " + typeOFval); }
                }
            }

            public override string ToString() { return this._tag.ToString() + ",Value={" + ((this.Value == null || this.Value.Length == 0) ? "\"\"" : (this.Value.Length == 1 ? this.Value[0].ToString() : SysTxt.Encoding.UTF8.GetString(this.Value))) + "},attr=" + this.ChildAttributes.Count.ToString(); }
            public T GetValue<T>() { return (T)SysConv.ChangeType(this.GetValue(), typeof(T)); }

            public byte[] GetBytes()
            {
                SysClG.List<byte> contentBytes = new SysClG.List<byte>();
                if (ChildAttributes.Count > 0)
                {
                    this._tag.IsConstructed = true;
                    foreach (LCore.LdapAttribute attr in this.ChildAttributes) { contentBytes.AddRange(attr.GetBytes()); }
                } else { contentBytes.AddRange(Value); }
                SysClG.List<byte> ret = new System.Collections.Generic.List<byte>(1);
                ret.Add(this._tag.TagByte);
                ret.AddRange(LCore.Utils.IntToBerLength(contentBytes.Count));
                ret.Capacity += contentBytes.Count;
                ret.AddRange(contentBytes);
                contentBytes.Clear();
                contentBytes = null;
                return ret.ToArray();
            }

            public virtual void Dispose()
            {
                this.Value = null;
                foreach (LCore.LdapAttribute attr in this.ChildAttributes) { attr.Dispose(); }
                this.ChildAttributes.Clear();
            }

            protected static SysClG.List<LCore.LdapAttribute> ParseAttributes(byte[] bytes, int currentPosition, int length)
            {
                SysClG.List<LCore.LdapAttribute> list = new SysClG.List<LCore.LdapAttribute>();
                while (currentPosition < length)
                {
                    LCore.Tag tag = LCore.Tag.Parse(bytes[currentPosition]);
                    currentPosition++;
                    int i = 0;
                    int attributeLength = LCore.Utils.BerLengthToInt(bytes, currentPosition, out i);
                    currentPosition += i;
                    LCore.LdapAttribute attribute = new LCore.LdapAttribute(tag);
                    if (tag.IsConstructed && attributeLength > 0) { attribute.ChildAttributes = ParseAttributes(bytes, currentPosition, currentPosition + attributeLength); }
                    else
                    {
                        attribute.Value = new byte[attributeLength];
                        Sys.Buffer.BlockCopy(bytes, currentPosition, attribute.Value, 0, attributeLength);
                    }
                    list.Add(attribute);
                    currentPosition += attributeLength;
                }
                return list;
            }

            protected LdapAttribute(LCore.Tag tag) { this._tag = tag; }
            public LdapAttribute(LCore.LdapOperation operation) { this._tag = new LCore.Tag(operation); }
            public LdapAttribute(LCore.LdapOperation operation, object value) : this(operation) { this.Value = this.GetBytes(value); }
            public LdapAttribute(LCore.UniversalDataType dataType) { this._tag = new LCore.Tag(dataType); }
            public LdapAttribute(LCore.UniversalDataType dataType, object value) : this(dataType) { this.Value = this.GetBytes(value); }
            public LdapAttribute(byte contextType) { this._tag = new LCore.Tag(contextType); }
            public LdapAttribute(byte contextType, object value) : this(contextType) { this.Value = this.GetBytes(value); }
        }

        public class LdapResultAttribute : LCore.LdapAttribute
        {
            public LdapResultAttribute(LCore.LdapOperation operation, LCore.LdapResult result, string matchedDN = "", string diagnosticMessage = "") : base(operation)
            {
                this.ChildAttributes.Add(new LCore.LdapAttribute(LCore.UniversalDataType.Enumerated, (byte)result));
                this.ChildAttributes.Add(string.IsNullOrEmpty(matchedDN) ? new LCore.LdapAttribute(LCore.UniversalDataType.OctetString, false) : new LCore.LdapAttribute(LCore.UniversalDataType.OctetString, matchedDN));
                this.ChildAttributes.Add(string.IsNullOrEmpty(diagnosticMessage) ? new LCore.LdapAttribute(LCore.UniversalDataType.OctetString, false) : new LCore.LdapAttribute(LCore.UniversalDataType.OctetString, diagnosticMessage));
            }
        }

        public class LdapPacket : LCore.LdapAttribute
        {
            public int MessageId { get { return this.ChildAttributes[0].GetValue<int>(); } }

            public static LCore.LdapPacket ParsePacket(byte[] bytes)
            {
                LCore.LdapPacket packet = new LCore.LdapPacket(LCore.Tag.Parse(bytes[0]));
                int lengthBytesCount = 0;
                int contentLength = LCore.Utils.BerLengthToInt(bytes, 1, out lengthBytesCount);
                packet.ChildAttributes.AddRange(LCore.LdapAttribute.ParseAttributes(bytes, 1 + lengthBytesCount, contentLength));
                return packet;
            }

            public static bool TryParsePacket(Sys.IO.Stream stream, out LCore.LdapPacket packet)
            {
                try
                {
                    if (stream.CanRead)
                    {
                        byte[] tagByte = new byte[1];
                        int i = stream.Read(tagByte, 0, 1);
                        if (i != 0)
                        {
                            int n = 0;
                            int contentLength = LCore.Utils.BerLengthToInt(stream, out n);
                            byte[] contentBytes = new byte[contentLength];
                            stream.Read(contentBytes, 0, contentLength);
                            packet = new LCore.LdapPacket(LCore.Tag.Parse(tagByte[0]));
                            packet.ChildAttributes.AddRange(LCore.LdapAttribute.ParseAttributes(contentBytes, 0, contentLength));
                            return true;
                        }
                    }
                } catch { /* NOTHING */ }
                packet = null;
                return false;
            }

            private LdapPacket(LCore.Tag tag) : base(tag) { /* NOTHING */ }
            public LdapPacket(int messageId) : base(LCore.UniversalDataType.Sequence) { this.ChildAttributes.Add(new LCore.LdapAttribute(LCore.UniversalDataType.Integer, messageId)); }
        }
    }

    internal struct SearchKey
    {
        internal string Key;
        internal string[] Values;

        internal SearchKey(string Key, string[] Values) : this()
        {
            this.Key = Key;
            this.Values = Values;
        }

        internal SearchKey(string Key, string Value) : this(Key, new string[] { Value }) { /* NOTHING */ }
    }

    internal struct SearchValue
    {
        internal string[] Keys;
        internal string Value;

        internal SearchValue(string[] Keys, string Value) : this()
        {
            this.Keys = Keys;
            this.Value = Value;
        }

        internal SearchValue(string Key, string Value) : this(new string[] { Key }, Value) { /* NOTHING */ }
    }

    internal class SearchCondition
    {
        internal LCore.LdapFilterChoice Filter = LCore.LdapFilterChoice.or;
        internal SysClG.List<LDap.SearchKey> Keys = new SysClG.List<LDap.SearchKey>(30);
    }

    public class Company
    {
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Country { get; set; }
        public string State { get; set; }
        public string City { get; set; }
        public string PostCode { get; set; }
        public string Address { get; set; }
    }

    public interface IDataList
    {
        SysClG.IEnumerable<LDap.IUserData> ListUsers();
        SysClG.IEnumerable<LDap.IGroup> ListGroups();
    }

    public interface INamed { string Name { get; } }
    public interface IGroup : LDap.IDataList, LDap.INamed { string BuildCN(); }

    public class Domain
    {
        protected LDap.IDataSource Source;
        public LDap.Company Company { get; set; }
        private string dc;
        private string pc;
        public string DomainCommon { get { return this.dc; } set { this.dc = (value == null ? "com" : value.ToLower()); } }
        public override string ToString() { return ("dc=" + this.NormalizedDC + ",dc=" + this.DomainCommon); }

        public string NormalizedDC
        {
            get
            {
                if (this.pc == null)
                {
                    this.pc = (this.Company == null ? "null" : this.Company.Name.Replace(' ', '.').Replace('\t', '.').Replace(';', '.').ToLower());
                    if (this.pc.EndsWith(".")) { this.pc = this.pc.Substring(0, (this.pc.Length - 1)); }
                }
                return this.pc;
            }
        }

        public Domain(LDap.IDataSource Source)
        {
            this.Source = Source;
            this.dc = "com";
        }
    }

    public interface IUserData : LDap.INamed
    {
        long UserID { get; }
        string FirstName { get; }
        string LastName { get; }
        string FullName { get; }
        string EMail { get; }
        LDap.IGroup Department { get; }
        string Job { get; }
        string Mobile { get; }
        bool TestPassword(string Password);
    }

    public interface IDataSource : LDap.IGroup
    {
        string AdminUser { get; }
        string AdminPassword { get; }
        LDap.Domain Domain { get; }
        bool Validate(string UserName, string Password, out bool IsAdmin);
    }

    public class Server : LServ.IServer //https://github.com/vforteli/Flexinets.Ldap.Server/blob/master/LdapServer.cs | https://docs.iredmail.org/use.openldap.as.address.book.in.outlook.html
    {
        public const int StandardPort = 389;
        public const string AccountClass = "SAMAccount";
        protected const string PosixAccountClass = "posixAccount";
        protected const string GroupAccountClass = "organizationalUnit";
        private SysSock.TcpListener _server;
        private bool _running;
        bool LServ.IServer.IsStarted { get { return this._running; } }
        private LDap.IDataSource _source;
        public LDap.IDataSource Source { get { return this._source; } }
        private bool IsValidType(ref string type) { return (type.ToLower() == "objectclass" || type == "top" || type == LDap.Server.AccountClass || type == LDap.Server.PosixAccountClass || type == LDap.Server.GroupAccountClass); }
        private LDap.SearchValue GetCompare(LDap.SearchValue[] pack, LDap.SearchKey key) { foreach (LDap.SearchValue val in pack) { foreach (string valKey in val.Keys) { if (valKey == key.Key) { return val; } } } return default(LDap.SearchValue); }
        private bool IsBind(LCore.LdapAttribute attr) { return (attr.LdapOperation == LCore.LdapOperation.BindRequest); }
        private bool IsSearch(LCore.LdapAttribute attr) { return (attr.LdapOperation == LCore.LdapOperation.SearchRequest); }
        private bool IsDisconnect(LCore.LdapAttribute attr) { return (attr.LdapOperation == LCore.LdapOperation.UnbindRequest || attr.LdapOperation == LCore.LdapOperation.AbandonRequest); }
        private string BuildCN(string AttributeSG, LDap.INamed named, LDap.IGroup Source) { return (AttributeSG + "=" + named.Name + "," + Source.BuildCN()); }
        private void WriteAttributes(byte[] pkB, SysSock.NetworkStream stream) { stream.Write(pkB, 0, pkB.Length); }
        private void WriteAttributes(LCore.LdapAttribute attr, SysSock.NetworkStream stream) { this.WriteAttributes(attr.GetBytes(), stream); }
        private LDap.IGroup FindGroup(string NormalizedNAME, LDap.IGroup Source) { foreach (LDap.IGroup grp in Source.ListGroups()) { if (grp.Name == NormalizedNAME) { return grp; } } return null; }
#if DEBUG
        private string LogThread() { return "{Thread;" + Sys.Threading.Thread.CurrentContext.ContextID.ToString() + ";" + Sys.Threading.Thread.CurrentThread.ManagedThreadId.ToString() + "}"; }
        private string LogDate() { return Sys.DateTime.Now.ToString(LServ.ServiceInstaller.LogDateFormat, Sys.Globalization.CultureInfo.InvariantCulture); }

        private void LogPacket(LCore.LdapAttribute pkO, int ident)
        {
            if (ident == 0) { Sys.Console.WriteLine("----------BEGIN----------"); }
            Sys.Console.WriteLine(new string('.', (3 + ident)) + " " + pkO.ToString());
            foreach (LCore.LdapAttribute pkI in pkO.ChildAttributes) { this.LogPacket(pkI, (ident + 1)); }
            if (ident == 0) { Sys.Console.WriteLine("-----------END-----------"); }
        }
#endif
        private int Matched(LDap.SearchValue[] pack, LDap.SearchKey key)
        {
            LDap.SearchValue comp = this.GetCompare(pack, key);
            if (comp.Keys == null || comp.Keys.Length == 0) { return -1; }
            else if (key.Values == null || key.Values.Length == 0 || (key.Values.Length == 1 && (key.Values[0] == "*" || key.Values[0] == ""))) { return 2; }
            else
            {
                int m = 0;
                foreach (string kv in key.Values) { if (comp.Value != null && comp.Value.IndexOf(kv, 0, Sys.StringComparison.CurrentCultureIgnoreCase) > -1) { m++; break; } }
                if (m == key.Values.Length) { m = 2; } else if (m > 0) { m = 1; }
                return m;
            }
        }

        public void Stop()
        {
            if (this._server != null) { this._server.Stop(); }
            this._running = false;
        }

        void Sys.IDisposable.Dispose()
        {
            this.Stop();
            this._server = null;
            this._running = false;
        }

        public void Start()
        {
            this._server.Start();
            this._running = true;
            this._server.BeginAcceptTcpClient(this.OnClientConnect, null);
        }

        private void AddAttribute(LCore.LdapAttribute partialAttributeList, string AttributeName, string AttributeValue)
        {
            if (!string.IsNullOrEmpty(AttributeValue))
            {
                LCore.LdapAttribute partialAttr = new LCore.LdapAttribute(LCore.UniversalDataType.Sequence);
                partialAttr.ChildAttributes.Add(new LCore.LdapAttribute(LCore.UniversalDataType.OctetString, AttributeName));
                LCore.LdapAttribute partialAttrVals = new LCore.LdapAttribute(LCore.UniversalDataType.Set);
                partialAttrVals.ChildAttributes.Add(new LCore.LdapAttribute(LCore.UniversalDataType.OctetString, AttributeValue));
                partialAttr.ChildAttributes.Add(partialAttrVals);
                partialAttributeList.ChildAttributes.Add(partialAttr);
            }
        }

        private void BuildPack(LCore.LdapAttribute holder, LDap.SearchValue[] pack)
        {
            LCore.LdapAttribute partialAttributeList = new LCore.LdapAttribute(LCore.UniversalDataType.Sequence);
            foreach (LDap.SearchValue pkItem in pack) { foreach (string Key in pkItem.Keys) { this.AddAttribute(partialAttributeList, Key, pkItem.Value); } }
            holder.ChildAttributes.Add(partialAttributeList);
        }

        private LDap.SearchValue[] UserPack(LDap.IUserData user)
        {
            LDap.SearchValue[] pk = new LDap.SearchValue[20];
            pk[0] = new LDap.SearchValue(new string[] { "cn", "commonName" }, user.Name);
            pk[1] = new LDap.SearchValue(new string[] { "uid", "id" }, user.UserID.ToString());
            pk[2] = new LDap.SearchValue(new string[] { "mail" }, user.EMail);
            pk[3] = new LDap.SearchValue(new string[] { "displayname", "display-name", "mailNickname", "mozillaNickname" }, user.FullName);
            pk[4] = new LDap.SearchValue(new string[] { "givenName" }, user.FirstName);
            pk[5] = new LDap.SearchValue(new string[] { "sn", "surname" }, user.LastName);
            pk[6] = new LDap.SearchValue(new string[] { "ou", "department" }, user.Department.Name);
            pk[7] = new LDap.SearchValue(new string[] { "co", "countryname" }, this._source.Domain.Company.Country);
            pk[8] = new LDap.SearchValue(new string[] { "postalAddress", "streetaddress" }, this._source.Domain.Company.Address);
            pk[9] = new LDap.SearchValue(new string[] { "company", "organizationName", "organizationUnitName" }, this._source.Domain.Company.Name);
            pk[10] = new LDap.SearchValue(new string[] { "objectClass" }, LDap.Server.AccountClass);
            pk[11] = new LDap.SearchValue(new string[] { "objectClass" }, LDap.Server.PosixAccountClass);
            pk[12] = new LDap.SearchValue(new string[] { "objectClass" }, "top");
            pk[13] = new LDap.SearchValue(new string[] { "title", "roleOccupant" }, user.Job);
            pk[14] = new LDap.SearchValue(new string[] { "telephoneNumber" }, this._source.Domain.Company.Phone);
            pk[15] = new LDap.SearchValue(new string[] { "l" }, this._source.Domain.Company.City);
            pk[16] = new LDap.SearchValue(new string[] { "st" }, this._source.Domain.Company.State);
            pk[17] = new LDap.SearchValue(new string[] { "postalCode" }, this._source.Domain.Company.PostCode);
            pk[18] = new LDap.SearchValue(new string[] { "mobile" }, user.Mobile);
            pk[19] = new LDap.SearchValue(new string[] { "initials" }, ((!string.IsNullOrEmpty(user.FirstName) && !string.IsNullOrEmpty(user.LastName)) ? (user.FirstName.Substring(0, 1) + user.LastName.Substring(0, 1)) : string.Empty));
            return pk;
        }

        private LDap.SearchValue[] GroupPack(LDap.IGroup grp)
        {
            LDap.SearchValue[] pk = new LDap.SearchValue[3];
            pk[0] = new LDap.SearchValue(new string[] { "ou", "organizationalUnitName", "cn", "commonName" }, grp.Name);
            pk[1] = new LDap.SearchValue(new string[] { "objectclass" }, LDap.Server.GroupAccountClass);
            pk[2] = new LDap.SearchValue(new string[] { "objectclass" }, "top");
            return pk;
        }

        private LCore.LdapPacket RespondCN(string CN, LDap.SearchValue[] pack, int MessageID)
        {
            LCore.LdapAttribute searchResultEntry = new LCore.LdapAttribute(LCore.LdapOperation.SearchResultEntry);
            searchResultEntry.ChildAttributes.Add(new LCore.LdapAttribute(LCore.UniversalDataType.OctetString, CN));
            this.BuildPack(searchResultEntry, pack);
            LCore.LdapPacket response = new LCore.LdapPacket(MessageID);
            response.ChildAttributes.Add(searchResultEntry);
            return response;
        }

        private void ReturnTrue(SysSock.NetworkStream stream, int MessageID)
        {
            LCore.LdapPacket pkO = new LCore.LdapPacket(MessageID);
            pkO.ChildAttributes.Add(new LCore.LdapAttribute(LCore.UniversalDataType.Boolean, true));
            pkO.ChildAttributes.Add(new LCore.LdapAttribute(LCore.UniversalDataType.Sequence));
            this.WriteAttributes(pkO, stream);
        }

        private void ReturnElements(SysSock.NetworkStream stream, int MessageID, int Limit, LDap.IGroup Source, LCore.Scope Scope)
        {
#if DEBUG
            Sys.Console.WriteLine(">>>> " + this.LogThread() + "ReturnAllUsers(NetworkStream,int,int) started at " + this.LogDate());
#endif
            foreach (LDap.IGroup grp in Source.ListGroups()) //'... appliy scope to list subgroups (don't know how yet)
            {
                if (Limit > 0)
                {
                    using (LCore.LdapPacket pkO = this.RespondCN(this.BuildCN("ou", grp, Source), this.GroupPack(grp), MessageID)) { this.WriteAttributes(pkO, stream); }
                    Limit--;
                } else { break; }
            }
            foreach (LDap.IUserData user in Source.ListUsers())
            {
                if (Limit > 0)
                {
                    using (LCore.LdapPacket pkO = this.RespondCN(this.BuildCN("cn", user, Source), this.UserPack(user), MessageID)) { this.WriteAttributes(pkO, stream); }
                    Limit--;
                } else { break; }
            }
#if DEBUG
            Sys.Console.WriteLine(">>> " + this.LogThread() + "ReturnAllUsers(NetworkStream,int,int) ended at " + this.LogDate());
#endif
        }

        private void ReturnSingleElement(SysSock.NetworkStream stream, int MessageID, ref string Name, LDap.IGroup Source, LCore.Scope Scope, bool IsGroup)
        {
#if DEBUG
            Sys.Console.WriteLine(">>>> " + this.LogThread() + "ReturnSingleUser(NetworkStream,int,string,bool) started at " + this.LogDate());
#endif
            if (!string.IsNullOrEmpty(Name))
            {
                Name = Name.ToLower();
                if (IsGroup)
                {
                    LDap.IGroup grp = this.FindGroup(Name, Source);
                    if (grp != null) { using (LCore.LdapPacket pkO = this.RespondCN(this.BuildCN("ou", grp, Source), this.GroupPack(grp), MessageID)) { this.WriteAttributes(pkO, stream); } }
                } else { foreach (LDap.IUserData user in Source.ListUsers()) { if (user.Name == Name) { using (LCore.LdapPacket pkO = this.RespondCN(this.BuildCN("cn", user, Source), this.UserPack(user), MessageID)) { this.WriteAttributes(pkO, stream); } } } }
            }
        }

        private string ExtractUser(string arg, ref string BindUR, out LDap.IGroup Source, out bool IsGroup)
        {
            Source = this._source;
            IsGroup = false;
            if (!string.IsNullOrEmpty(arg))
            {
                if (arg == BindUR) { arg = string.Empty; }
                else
                {
                    arg = arg.Trim().Replace(BindUR, string.Empty).ToLower();
                    if (arg.EndsWith(",")) { arg = arg.Substring(0, (arg.Length - 1)); }
                    if (arg.StartsWith("cn=") || arg.StartsWith("ou=") || arg.StartsWith("id=")) { arg = arg.Substring(3); } else if (arg.StartsWith("uid=")) { arg = arg.Substring(4); }
                    int cIDX = arg.LastIndexOf(',');
                    if (cIDX != -1)
                    {
                        string AUX = string.Empty;
                        while (cIDX != -1)
                        {
                            AUX = arg.Substring(cIDX + 1);
                            arg = arg.Substring(0, cIDX);
                            if (AUX.StartsWith("cn=") || AUX.StartsWith("ou=") || AUX.StartsWith("id=")) { AUX = AUX.Substring(3); } else if (AUX.StartsWith("uid=")) { AUX = AUX.Substring(4); }
                            Source = this.FindGroup(AUX, Source);
                            if (Source == null) //FAIL
                            {
                                Source = this._source;
                                arg = string.Empty;
                                break;
                            } else { cIDX = arg.LastIndexOf(','); }
                        }
                    }
                    if (!string.IsNullOrEmpty(arg)) { IsGroup = (this.FindGroup(arg, Source) != null); }
                }
            }
            return arg;
        }

        private LDap.SearchCondition GetSearchOptions(LCore.LdapAttribute filter)
        {
            LDap.SearchKey cur = new LDap.SearchKey("*", filter.GetValue<string>());
            LDap.SearchCondition args = new LDap.SearchCondition();
            try
            {
                args.Filter = (LCore.LdapFilterChoice)filter.ContextType;
                if (string.IsNullOrEmpty(cur.Values[0]))
                {
                    if (filter.ChildAttributes.Count == 1) { filter = filter.ChildAttributes[0]; }
                    if (filter.ChildAttributes.Count > 0)
                    {
                        args.Filter = (LCore.LdapFilterChoice)filter.ContextType;
                        string[] nARG = null;
                        LCore.LdapAttribute varg = null;
                        foreach (LCore.LdapAttribute arg in filter.ChildAttributes)
                        {
                            if (arg.ChildAttributes.Count == 2 && arg.ChildAttributes[0].DataType == LCore.UniversalDataType.OctetString)
                            {
                                cur = new LDap.SearchKey(arg.ChildAttributes[0].GetValue<string>(), (null as string[]));
                                varg = arg.ChildAttributes[1];
                                if (varg.DataType == LCore.UniversalDataType.OctetString) { cur.Values = new string[] { varg.GetValue<string>() }; }
                                else
                                {
                                    nARG = new string[varg.ChildAttributes.Count];
                                    for (int i = 0; i < varg.ChildAttributes.Count; i++) { nARG[i] = varg.ChildAttributes[i].GetValue<string>(); }
                                    cur.Values = nARG;
                                    nARG = null;
                                }
                                if (!string.IsNullOrEmpty(cur.Key)) { args.Keys.Add(cur); }
                            }
                        }
                    }
                } else { args.Keys.Add(cur); }
            }
#if DEBUG
            catch (Sys.Exception e)
            {
                args.Keys.Clear();
                Sys.Console.WriteLine(">>>>> " + this.LogThread() + "GetSearchOptions(LdapAttribute) error at " + this.LogDate() + ", exception: " + e.Message);
            }
#else
            catch { args.Keys.Clear(); }
#endif
            return args;
        }

        private bool Matched(LDap.SearchValue[] pack, LDap.SearchCondition args)
        {
            int mcount = -1;
            switch (args.Filter)
            {
                case LCore.LdapFilterChoice.or:
                    foreach (LDap.SearchKey key in args.Keys)
                    {
                        mcount = this.Matched(pack, key);
                        if (mcount > 0) { return true; }
                    }
                    break;
                case LCore.LdapFilterChoice.and:
                    bool Matched = true;
                    if (args.Keys.Count == pack.Length) //Since all must match anyway
                    {
                        Matched = true;
                        foreach (LDap.SearchKey key in args.Keys)
                        {
                            mcount = this.Matched(pack, key);
                            if (mcount != 2) { Matched = false; break; }
                        }
                    }
                    return Matched;
            }
            return false;
        }

        private void ReturnMatchs(SysSock.NetworkStream stream, int MessageID, int Limit, LDap.SearchCondition args, LDap.IGroup Source)
        {
#if DEBUG
            Sys.Console.WriteLine(">>> " + this.LogThread() + "ReturnUsers(NetworkStream,int,int,SearchCondition) started at " + this.LogDate());
#endif
            LDap.SearchValue[] pack = null;
            foreach (LDap.IGroup grp in Source.ListGroups())
            {
                if (Limit > 0)
                {
                    pack = this.GroupPack(grp);
                    if (this.Matched(pack, args))
                    {
                        using (LCore.LdapPacket pkO = this.RespondCN(this.BuildCN("ou", grp, Source), pack, MessageID)) { this.WriteAttributes(pkO, stream); }
                        Limit--;
                    }
                } else { break; }
            }
            foreach (LDap.IUserData user in Source.ListUsers())
            {
                if (Limit > 0)
                {
                    pack = this.UserPack(user);
                    if (this.Matched(pack, args))
                    {
                        using (LCore.LdapPacket pkO = this.RespondCN(this.BuildCN("cn", user, Source), pack, MessageID)) { this.WriteAttributes(pkO, stream); }
                        Limit--;
                    }
                } else { break; }
            }
#if DEBUG
            Sys.Console.WriteLine(">>> " + this.LogThread() + "ReturnUsers(NetworkStream,int,int,SearchCondition) ended at " + this.LogDate());
#endif
        }

        private void ReturnHello(SysSock.NetworkStream stream, int MessageID, LCore.LdapPacket responsePacket, int Limit, LDap.IGroup Source)
        {
#if DEBUG
            Sys.Console.WriteLine(">>> " + this.LogThread() + "ReturnHello(NetworkStream,int,LdapPacket,int) started at " + this.LogDate());
#endif
            LDap.SearchValue[] pack = new LDap.SearchValue[5];
            pack[0] = new LDap.SearchValue("objectclass", "top");
            pack[1] = new LDap.SearchValue("objectclass", "dcObject");
            pack[2] = new LDap.SearchValue("objectclass", "organization");
            pack[3] = new LDap.SearchValue("o", (this._source.Domain.NormalizedDC + "." + this._source.Domain.DomainCommon));
            pack[4] = new LDap.SearchValue("dc", this._source.Domain.NormalizedDC);
            this.WriteAttributes(this.RespondCN(this._source.Domain.ToString(), pack, MessageID), stream);
            this.ReturnElements(stream, MessageID, Limit, Source, LCore.Scope.baseObject);
#if DEBUG
            Sys.Console.WriteLine(">>> " + this.LogThread() + "ReturnHello(NetworkStream,int,LdapPacket,int) ended at " + this.LogDate());
#endif
        }

        private void HandleSearchRequest(SysSock.NetworkStream stream, LCore.LdapPacket requestPacket, bool IsAdmin)
        {
#if DEBUG
            Sys.Console.WriteLine(">>> " + this.LogThread() + "HandleSearchRequest(NetworkStream,LdapPacket,bool) started at " + this.LogDate());
            this.LogPacket(requestPacket, 0);
#endif
            LCore.LdapAttribute searchRequest = LCore.Utils.SingleOrDefault<LCore.LdapAttribute>(requestPacket.ChildAttributes, this.IsSearch);
            LCore.LdapPacket responsePacket = new LCore.LdapPacket(requestPacket.MessageId);
            if (searchRequest == null || searchRequest.ChildAttributes.Count < 7) { responsePacket.ChildAttributes.Add(new LCore.LdapResultAttribute(LCore.LdapOperation.SearchResultDone, LCore.LdapResult.compareFalse)); }
            else
            {
                string arg = string.Empty;
                LCore.Scope scope = LCore.Scope.baseObject;
                try
                {
                    if (searchRequest.ChildAttributes[1].DataType == LCore.UniversalDataType.Integer) { scope = (LCore.Scope)searchRequest.ChildAttributes[1].GetValue<int>(); }
                    else
                    {
                        switch (searchRequest.ChildAttributes[1].Value[0])
                        {
                            case 1: scope = LCore.Scope.singleLevel; break;
                            case 2: scope = LCore.Scope.wholeSubtree; break;
                            default: scope = LCore.Scope.baseObject; break;
                        }
                    }
                } catch { /* NOTHING */ }
                int limit = searchRequest.ChildAttributes[3].GetValue<int>();
                if (limit == 0) { limit = 999; } //max on outlook/thunderbird | target clients
                arg = searchRequest.ChildAttributes[0].GetValue<string>();
                if (arg != null) { arg = arg.ToLower(); }
                LCore.LdapAttribute filter = searchRequest.ChildAttributes[6];
                LCore.LdapFilterChoice filterMode = (LCore.LdapFilterChoice)filter.ContextType;
                string BindUR = this._source.Domain.ToString();
                if (arg != null && arg.Contains(BindUR))
                {
                    LDap.IGroup Source = null;
                    bool IsGroup = false;
                    arg = this.ExtractUser(arg, ref BindUR, out Source, out IsGroup);
                    switch (filterMode)
                    {
                        case LCore.LdapFilterChoice.equalityMatch:
                        case LCore.LdapFilterChoice.present:
                            if (arg == null || arg == string.Empty)
                            {
                                if (Source == this._source) { this.ReturnHello(stream, requestPacket.MessageId, responsePacket, limit, Source); }
                                else
                                {
                                    arg = filter.GetValue<string>();
                                    this.ReturnSingleElement(stream, requestPacket.MessageId, ref arg, Source, scope, IsGroup);
                                }
                            } else if (scope == LCore.Scope.baseObject) { this.ReturnSingleElement(stream, requestPacket.MessageId, ref arg, Source, scope, IsGroup); }
                            else if (IsGroup) { this.ReturnElements(stream, requestPacket.MessageId, limit, this.FindGroup(arg, Source), scope); }
                            break;
                        case LCore.LdapFilterChoice.and:
                        case LCore.LdapFilterChoice.or:
                            if (string.IsNullOrEmpty(arg) || this.IsValidType(ref arg))
                            {
                                LDap.SearchCondition args = this.GetSearchOptions(filter);
                                if (args.Keys.Count == 0 || args.Keys[0].Key == "*") { this.ReturnElements(stream, requestPacket.MessageId, limit, Source, scope); } else { this.ReturnMatchs(stream, requestPacket.MessageId, limit, args, Source); }
                            } else { this.ReturnSingleElement(stream, requestPacket.MessageId, ref arg, Source, scope, IsGroup); }
                            break;
                    }
                }
                else
                {
                    arg = filter.GetValue<string>();
                    if (!string.IsNullOrEmpty(arg))
                    {
                        switch (filterMode)
                        {
                            case LCore.LdapFilterChoice.present: if (this.IsValidType(ref arg)) { this.ReturnTrue(stream, requestPacket.MessageId); } break;
                            default: break; //NOTHING YET!
                        }
                    }
                }
                responsePacket.ChildAttributes.Add(new LCore.LdapResultAttribute(LCore.LdapOperation.SearchResultDone, LCore.LdapResult.success));
            }
#if DEBUG
            Sys.Console.WriteLine(">>> " + this.LogThread() + "HandleSearchRequest(NetworkStream,LdapPacket,bool) done at " + this.LogDate());
#endif
            this.WriteAttributes(responsePacket, stream);
        }

        private bool HandleBindRequest(SysSock.NetworkStream stream, LCore.LdapPacket requestPacket, out bool IsAdmin)
        {
#if DEBUG
            Sys.Console.WriteLine(">>> " + this.LogThread() + "HandleBindRequest(NetworkStream,LdapPacket,out bool) started at " + this.LogDate());
#endif
            IsAdmin = false;
            LCore.LdapAttribute bindrequest = LCore.Utils.SingleOrDefault<LCore.LdapAttribute>(requestPacket.ChildAttributes, o => { return o.LdapOperation == LCore.LdapOperation.BindRequest; });
            if (bindrequest == null)
            {
#if DEBUG
                Sys.Console.WriteLine(">>> " + this.LogThread() + "HandleBindRequest(NetworkStream,LdapPacket,out bool) completed as FALSE at " + this.LogDate());
#endif
                return false;
            }
            else
            {
                LDap.IGroup Source = null;
                string AUX = this._source.Domain.ToString();
                bool IsGroup = false;
                string username = this.ExtractUser(bindrequest.ChildAttributes[1].GetValue<string>(), ref AUX, out Source, out IsGroup);
                LCore.LdapResult response = LCore.LdapResult.invalidCredentials;
                if (!IsGroup) //Groups do not login
                {
                    AUX = bindrequest.ChildAttributes[2].GetValue<string>();
                    if (this._source.Validate(username, AUX, out IsAdmin)) { response = LCore.LdapResult.success; }
                }
                LCore.LdapPacket responsePacket = new LCore.LdapPacket(requestPacket.MessageId);
                responsePacket.ChildAttributes.Add(new LCore.LdapResultAttribute(LCore.LdapOperation.BindResponse, response));
                this.WriteAttributes(responsePacket, stream);
#if DEBUG
                Sys.Console.WriteLine(">>> " + this.LogThread() + "HandleBindRequest(NetworkStream,LdapPacket,out bool) completed with response " + response.ToString() + " at " + this.LogDate());
#endif
                return (response == LCore.LdapResult.success);
            }
        }

        private void HandleClient(SysSock.TcpClient client)
        {
#if DEBUG
            Sys.Console.WriteLine(">>> " + this.LogThread() + "HandleClient(TcpClient) started at " + this.LogDate());
#endif
            this._server.BeginAcceptTcpClient(this.OnClientConnect, null);
            try
            {
                bool isBound = false;
                bool IsAdmin = false;
                bool nonSearch = true;
#if DEBUG
                Sys.Console.WriteLine(">>> " + this.LogThread() + "HandleClient(TcpClient), will call GetStream() at " + this.LogDate());
#endif
                SysSock.NetworkStream stream = client.GetStream();
#if DEBUG
                Sys.Console.WriteLine(">>> " + this.LogThread() + "HandleClient(TcpClient), GetStream() completed at " + this.LogDate());
                long loopCount = 0;
#endif
                LCore.LdapPacket requestPacket = null;
                while (LCore.LdapPacket.TryParsePacket(stream, out requestPacket))
                {
#if DEBUG
                    loopCount++;
                    Sys.Console.WriteLine(">>> " + this.LogThread() + "HandleClient(TcpClient), PacketParsed (" + loopCount.ToString() + ") at " + this.LogDate());
#endif
                    if (LCore.Utils.Any<LCore.LdapAttribute>(requestPacket.ChildAttributes, this.IsDisconnect))
                    {
#if DEBUG
                        Sys.Console.WriteLine(">>> " + this.LogThread() + "HandleClient(TcpClient), Abandon/Unbind Request received (" + loopCount.ToString() + ") at " + this.LogDate());
#endif
                        break;
                    }
                    else
                    {
                        if (LCore.Utils.Any<LCore.LdapAttribute>(requestPacket.ChildAttributes, this.IsBind)) { isBound = this.HandleBindRequest(stream, requestPacket, out IsAdmin); }
                        if (isBound && LCore.Utils.Any<LCore.LdapAttribute>(requestPacket.ChildAttributes, this.IsSearch))
                        {
                            nonSearch = false;
                            this.HandleSearchRequest(stream, requestPacket, IsAdmin);
                        }
                    }
                }
#if DEBUG
                Sys.Console.WriteLine(">>> " + this.LogThread() + "HandleClient(TcpClient), Packet parse completed at " + this.LogDate());
#endif
                if (nonSearch && (!isBound) && (requestPacket != null))
                {
                    LCore.LdapPacket responsePacket = new LCore.LdapPacket(requestPacket.MessageId);
                    responsePacket.ChildAttributes.Add(new LCore.LdapResultAttribute(LCore.LdapOperation.CompareResponse, LCore.LdapResult.compareFalse));
                    this.WriteAttributes(responsePacket, stream);
                }
#if DEBUG
            } catch (Sys.Exception e) { Sys.Console.WriteLine(">>> " + this.LogThread() + "HandleClient(TcpClient) error at " + this.LogDate() + ", exception: " + e.Message); }
#else
            } catch { /* NOTHING */ }
#endif
        }

        private void OnClientConnect(Sys.IAsyncResult asyn)
        {
#if DEBUG
            Sys.Console.WriteLine();
            Sys.Console.WriteLine("> Thread: " + this.LogThread());
            Sys.Console.WriteLine(">> " + this.LogThread() + "OnClientConnect(IAsyncResult) called");
#endif
            try { if (this._server != null) { this.HandleClient(this._server.EndAcceptTcpClient(asyn)); } }
#if DEBUG
            catch (Sys.Exception e) { Sys.Console.WriteLine(">> " + this.LogThread() + "OnClientConnect(IAsyncResult) error at " + this.LogDate() + ", exception: " + e.Message); }
            Sys.Console.WriteLine(">> " + this.LogThread() + "OnClientConnect(IAsyncResult) ended");
#else
            catch { /* NOTHING */ }
#endif
        }
        
        protected Server(Sys.Net.IPEndPoint localEndpoint) { this._server = new SysSock.TcpListener(localEndpoint); }
        public Server(LDap.IDataSource Validator, Sys.Net.IPEndPoint localEndpoint) : this(localEndpoint) { this._source = Validator; }
        public Server(LDap.IDataSource Validator, string localEndpoint, int Port) : this(new Sys.Net.IPEndPoint(Sys.Net.IPAddress.Parse(localEndpoint), Port)) { this._source = Validator; }
        public Server(LDap.IDataSource Validator, string localEndpoint) : this(Validator, localEndpoint, LDap.Server.StandardPort) { /* NOTHING */ }
    }
}
