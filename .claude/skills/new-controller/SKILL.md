---
name: new-controller
description: Create a new API Controller following CoffeeShopApi conventions
---

Create a new Controller in Controllers/ following these conventions:

- Inherit from ControllerBase
- Add [ApiController] and [Route("api/[controller]")]
- Constructor-inject: CoffeeShopDbContext, UserManager<ApplicationUser> (if needed), ILogger<T>
- Add [Authorize] at class or action level depending on route sensitivity
- Annotate all actions with [ProducesResponseType]
- Return ActionResult<T>, not IActionResult
- All actions async (async Task<ActionResult<T>>)
- Naming convention: {Resource}Controller.cs

Controller to create: {{args}}
