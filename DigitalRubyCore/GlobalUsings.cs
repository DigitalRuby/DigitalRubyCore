global using System.Collections;
global using System.Collections.Concurrent;
global using System.ComponentModel;
global using System.Diagnostics;
global using System.Globalization;
global using System.Linq.Expressions;
global using System.Reflection;
global using System.Runtime.CompilerServices;
global using System.Security.Claims;
global using System.Text;
global using System.Text.Encodings.Web;

global using Microsoft.AspNetCore.Authorization;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore.Storage;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using Microsoft.IdentityModel.Tokens;

global using FeatureFlags.Core.Authentication;
global using FeatureFlags.Core.Cryptography;
global using FeatureFlags.Core.DependencyInjection;
global using FeatureFlags.Core.Logging;
global using FeatureFlags.Core.Networking;
global using FeatureFlags.Core.Reflection;

global using Newtonsoft.Json;