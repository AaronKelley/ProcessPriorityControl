using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace ProcessPriorityControl.Cmd
{
    /// <summary>
    /// Contains methods for dealing with Windows user accounts.
    /// </summary>
    static class UserUtility
    {
        /// <summary>
        /// Take a UserInformation object with the SID filled in, and fill in the username fields.
        /// </summary>
        /// <param name="information">UserInformation object</param>
        /// <seealso cref="https://www.morgantechspace.com/2015/09/convert-sid-to-username-using-c-sharp.html"/>
        public static void FillUsernameFromSid(UserInformation information)
        {
            StringBuilder name = new StringBuilder();
            StringBuilder referencedDomainName = new StringBuilder();

            uint cchName = uint.Parse(name.Capacity.ToString());
            uint cchReferencedDomainName = uint.Parse(referencedDomainName.Capacity.ToString());

            SidNameUse sidUse;

            SecurityIdentifier sid = new SecurityIdentifier(information.Sid);
            byte[] byteSid = new byte[sid.BinaryLength];
            sid.GetBinaryForm(byteSid, 0);

            int error = NoError;
            if (!LookupAccountSid(null, byteSid, name, ref cchName, referencedDomainName, ref cchReferencedDomainName, out sidUse))
            {
                error = Marshal.GetLastWin32Error();
                if (error == ErrorInsufficientBuffer)
                {
                    name.EnsureCapacity(int.Parse(cchName.ToString()));
                    referencedDomainName.EnsureCapacity(int.Parse(cchReferencedDomainName.ToString()));
                    error = NoError;
                    if (!LookupAccountSid(null, byteSid, name, ref cchName, referencedDomainName, ref cchReferencedDomainName, out sidUse))
                    {
                        error = Marshal.GetLastWin32Error();
                    }
                }
            }
            if (error == NoError)
            {
                information.Username = name.ToString();
                information.Domain = referencedDomainName.ToString();
            }
            else
            {
                throw new Exception("Failed with error: " + error);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // The following structs, enums, and external methods are used by code in this class to pull data from the Win32 API. //
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private static readonly int NoError = 0;

        const int ErrorInsufficientBuffer = 122;

        enum SidNameUse
        {
            SidTypeUser = 1,
            SidTypeGroup,
            SidTypeDomain,
            SidTypeAlias,
            SidTypeWellKnownGroup,
            SidTypeDeletedAccount,
            SidTypeInvalid,
            SidTypeUnknown,
            SidTypeComputer
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool LookupAccountSid(
            string lpSystemName,
            [MarshalAs(UnmanagedType.LPArray)] byte[] Sid,
            StringBuilder lpName,
            ref uint cchName,
            StringBuilder ReferencedDomainName,
            ref uint cchReferencedDomainName,
            out SidNameUse peUse
        );
    }
}
