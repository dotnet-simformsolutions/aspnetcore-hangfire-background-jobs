using Hangfire.Dashboard;

namespace HangfireOrderDemo.Security;

/// <summary>
/// Guards the dashboard outside Development. Program.cs wires this up in the
/// non-Development branch.
///
/// It denies everyone until you add authentication, which is deliberate: the
/// dashboard can retry and delete jobs, so failing closed is the safe default.
/// To use it, add an authentication scheme (cookie, OpenID Connect, ...) and
/// call UseAuthentication() and UseAuthorization() BEFORE UseHangfireDashboard,
/// or HttpContext.User is never populated and this filter rejects every request.
///
/// Why not rely on Hangfire's built-in local-only filter in production: that
/// filter is a plain IP check. Behind a reverse proxy, a sidecar or an ingress,
/// the remote address is the proxy's - often loopback - so every request from
/// the internet looks local and is let through.
///
/// For policy-based authorization, Hangfire.AspNetCore also offers
/// MapHangfireDashboardWithAuthorizationPolicy, and IDashboardAsyncAuthorizationFilter
/// for checks that need to await something.
/// </summary>
public class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var user = context.GetHttpContext().User;

        // Support and Admin can open the dashboard; Program.cs makes it
        // read-only for anyone who is not Admin. Replace with your own rule.
        return user.Identity?.IsAuthenticated == true
            && (user.IsInRole("Admin") || user.IsInRole("Support"));
    }
}
