using System.Net;
using System.Net.Http;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace DigitalRuby.Core.Tests.Authentication;

public class JwtTests
{
	/// <summary>
	/// Test jwt tokens are working
	/// </summary>
	/// <returns>Task</returns>
	[Test]
	public async Task TestJwtBearer()
	{
		var hostBuilder = new WebHostBuilder()
			.UseTestServer()
			.ConfigureServices(services =>
			{
				services.AddLogging();
				services.AddJwtAuthentication(File.ReadAllText("./TestData/Authentication/JwtPublicKey.txt"),
				File.ReadAllText("./TestData/Authentication/JwtPrivateKey.txt"),
				"https://localhost",
				"localhost",
				"token");
			})
			.Configure(app =>
			{
				app.UseAuthentication();
				app.UseAuthorization();
				app.Use(async (HttpContext ctx, Func<Task> next) =>
				{
					var result = await ctx.AuthenticateAsync();
					if (!result.Succeeded || ctx.User.Identity?.Name != "1")
					{
						ctx.Response.StatusCode = 401;
					}
					await ctx.Response.CompleteAsync();
				});
			});
		using var host = hostBuilder.Build();
		host.RunAsync().GetAwaiter();
		var client = host.GetTestClient();
		var response = await client.GetAsync("/");
		Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);

		var token = host.Services.GetRequiredService<IJwtTokenService>().CreateToken(new CreateTokenRequest("1", "poo"));
		var request = new HttpRequestMessage() { Method = HttpMethod.Get };
		request.Headers.Add("Authorization", "Bearer " + token);
		response = await client.SendAsync(request);
		Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

		request = new HttpRequestMessage() { Method = HttpMethod.Get };
		request.Headers.Add("Cookie", "token=" + token);
		response = await client.SendAsync(request);
		Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
	}
}
