using Lumify.Api.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Lumify.Api.Controllers
{
    public abstract class BaseController : ControllerBase
    {
        protected Guid UserId => User.GetId();
        protected string Email => User.GetEmail();
    }
}
