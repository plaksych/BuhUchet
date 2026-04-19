using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BuhUchet
{
    public class UserService
    {
        private static readonly string UsersFile =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "users.json");

        private List<User> _users = new();

        public UserService()
        {
            Load();
        }

        // ── Хэш пароля SHA-256 ──
        private static string Hash(string password)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes).ToLower();
        }

        // ── Загрузка из файла ──
        private void Load()
        {
            if (!File.Exists(UsersFile)) return;
            try
            {
                var json = File.ReadAllText(UsersFile);
                _users = JsonSerializer.Deserialize<List<User>>(json) ?? new();
            }
            catch { _users = new(); }
        }

        // ── Сохранение в файл ──
        private void Save()
        {
            var json = JsonSerializer.Serialize(_users,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(UsersFile, json);
        }

        // ── Регистрация ──
        public (bool Success, string Error) Register(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
                return (false, "Введите имя пользователя.");

            if (password.Length < 4)
                return (false, "Пароль должен быть не менее 4 символов.");

            if (_users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                return (false, "Пользователь с таким именем уже существует.");

            _users.Add(new User
            {
                Username = username.Trim(),
                PasswordHash = Hash(password)
            });
            Save();
            return (true, "");
        }

        // ── Вход ──
        public (bool Success, string Error) Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return (false, "Заполните все поля.");

            var user = _users.FirstOrDefault(u =>
                u.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase));

            if (user == null || user.PasswordHash != Hash(password))
                return (false, "Неверное имя пользователя или пароль.");

            return (true, "");
        }

        public bool HasUsers() => _users.Count > 0;
    }
}