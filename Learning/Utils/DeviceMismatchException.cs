namespace LearningBackendAPI.Utils
{
    // Thrown when a student logs in from a device other than the one registered to the account
    public class DeviceMismatchException : Exception
    {
        public DeviceMismatchException() : base(Constants.Messages.DeviceMismatch) { }
    }
}
