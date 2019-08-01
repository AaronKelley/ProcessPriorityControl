namespace ProcessPriorityControl.Cmd
{
    /// <summary>
    /// This class represents a Windows user.
    /// </summary>
    class UserInformation
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="username">Windows user name</param>
        /// <param name="domain">Account domain</param>
        /// <param name="sid">User account identifier (SID)</param>
        public UserInformation(string username, string domain, string sid)
        {
            Username = username;
            Domain = domain;
            Sid = sid;
        }

        /// <summary>
        /// Windows user name
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Account domain
        /// </summary>
        public string Domain { get; set; }

        /// <summary>
        /// User account identifier (SID)
        /// </summary>
        public string Sid { get; }
    }
}
