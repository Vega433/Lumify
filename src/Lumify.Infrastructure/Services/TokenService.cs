using Lumify.Application.DTOs;
using Lumify.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Lumify.Infrastructure.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateAccessToken(UserAuthDto authDto)
        {
            // NameIdentifier — то, что потом читает BaseController
            List<Claim> claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, authDto.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, authDto.UserId.ToString()),
                new Claim(ClaimTypes.Email, authDto.Email),
                new Claim(JwtRegisteredClaimNames.Email, authDto.Email),
                new Claim("username", authDto.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            SigningCredentials credit = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            SecurityTokenDescriptor tokenDescriptor = new SecurityTokenDescriptor {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(30),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = credit
            };

            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            byte[] randomNumber = new byte[32];
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);

            return Convert.ToBase64String(randomNumber);
        }
    }
}
