using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DMicroservices.Base.Identity
{
    public abstract class IdentityFilter : ActionFilterAttribute, IAuthorizationFilter
    {
        public virtual void OnAuthorization(AuthorizationFilterContext context)
        {
            
        }
    }
}
