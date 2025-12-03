using System.Threading.Tasks;

namespace App.Services
{
    [System.Serializable]
    public class UserDto
    {
        public long userId;
        public string email;
        public string name;
        public string role; // GENERAL / ADMIN / VISITOR
    }

    public interface IUserService
    {
        Task<(bool ok, UserDto user, string message)> GetMeAsync();
    }
}
