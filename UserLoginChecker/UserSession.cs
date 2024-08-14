namespace UserLoginChecker
{
    public struct UserSession
    {
        public string Username { get; set; }
        public string SessionName { get; set; }
        public string Id { get; set; }
        public string State { get; set; }
        public string IdleTime { get; set; }
        public string LogonTime { get; set; }
        public string ComputerName { get; set; }
    }
}
