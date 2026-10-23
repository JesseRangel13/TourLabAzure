# Observed HTTP and UI declarations

Read-only source scan, 2026-10-23. Paths refer to the original repository. These declarations are inventory, not an executed API test. Group prefixes and authorization must be read in the owning endpoint file. No source secrets or generated assets were imported.

C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\CatalogEndpoints.cs:138:        human.MapGet("/tours", (string? search, int? page, int? pageSize, HttpContext http, CatalogQueries queries, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\CatalogEndpoints.cs:141:        human.MapGet("/tours/{id:guid}", (Guid id, HttpContext http, CatalogQueries queries, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\CatalogEndpoints.cs:144:        human.MapGet("/tours/{id:guid}/sessions", (Guid id, HttpContext http, CatalogQueries queries, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\CatalogEndpoints.cs:149:        admin.MapGet("/faults", async (CatalogDb db, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\CatalogEndpoints.cs:160:        admin.MapPut("/faults", (FaultSelection request, HttpContext http, CatalogDb db, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\CatalogEndpoints.cs:163:        admin.MapPost("/faults/reset", (HttpContext http, CatalogDb db, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\CatalogEndpoints.cs:166:        admin.MapPost("/tours", async (CreateTour command, CatalogCommands handler, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\CatalogEndpoints.cs:172:        admin.MapPut("/tours/{id:guid}", (Guid id, UpdateTour command, CatalogCommands handler, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\CatalogEndpoints.cs:175:        admin.MapPost("/tours/{id:guid}/sessions", async (Guid id, CreateSession command, CatalogCommands handler, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\CatalogEndpoints.cs:181:        admin.MapPut("/sessions/{id:guid}/price", (Guid id, UpdatePrice command, CatalogCommands handler, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\CatalogEndpoints.cs:184:        admin.MapPut("/sessions/{id:guid}/capacity", (Guid id, UpdateCapacity command, CatalogCommands handler, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\CatalogEndpoints.cs:187:        api.MapPost("/internal/quotes", (CreateQuote command, QuoteHandler handler, Shared.Infrastructure.Operations.ServiceOperations operations, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Operations\ServiceOperations.cs:201:        admin.MapGet("/diagnostics",(ServiceOperations operations,int? page,int? pageSize,CancellationToken ct)=>operations.DiagnosticsAsync(page??1,pageSize??10,ct));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Operations\ServiceOperations.cs:202:        admin.MapGet("/dead-letters",(ServiceOperations operations,HttpContext http,int? pageSize,CancellationToken ct)=>operations.InspectAsync(pageSize??10,Actor(http),ct));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Operations\ServiceOperations.cs:203:        admin.MapPost("/dead-letters/{deliveryId:guid}/replay",(Guid deliveryId,ServiceOperations operations,HttpContext http,CancellationToken ct)=>operations.ReplayAsync(deliveryId,Actor(http),ct));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Operations\ServiceOperations.cs:206:            admin.MapGet("/faults",(ServiceOperations operations,HttpContext http,CancellationToken ct)=>operations.ReservationFaultAsync(null,Actor(http),ct));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Operations\ServiceOperations.cs:207:            admin.MapPut("/faults",(FaultSelection request,ServiceOperations operations,HttpContext http,CancellationToken ct)=>operations.ReservationFaultAsync(request,Actor(http),ct));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Operations\ServiceOperations.cs:208:            admin.MapPost("/faults/reset",(ServiceOperations operations,HttpContext http,CancellationToken ct)=>operations.ReservationFaultAsync(new("None",0),Actor(http),ct));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Notifications.Api\NotificationEndpoints.cs:30:        root.MapGet("/notifications",async(HttpContext http,NotificationsDb db,Guid? reservationId,int? page,int? pageSize,CancellationToken ct)=>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Notifications.Api\NotificationEndpoints.cs:37:        root.MapGet("/notifications/{id:guid}",async(Guid id,HttpContext http,NotificationsDb db,CancellationToken ct)=>View(
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Notifications.Api\NotificationEndpoints.cs:42:        admin.MapGet("/faults",async(NotificationsDb db,CancellationToken ct)=>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Notifications.Api\NotificationEndpoints.cs:44:        admin.MapPut("/faults",(FaultSelection request,HttpContext http,NotificationsDb db,CancellationToken ct)=>SetFaultAsync(app,http,db,request,ct));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Notifications.Api\NotificationEndpoints.cs:45:        admin.MapPost("/faults/reset",(HttpContext http,NotificationsDb db,CancellationToken ct)=>SetFaultAsync(app,http,db,new("None",0),ct));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Payments.Api\PaymentEndpoints.cs:32:        root.MapGet("/payments",async(HttpContext http,PaymentsDb db,Guid? reservationId,int? page,int? pageSize,CancellationToken ct)=>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Payments.Api\PaymentEndpoints.cs:39:        root.MapGet("/payments/{id:guid}",async(Guid id,HttpContext http,PaymentsDb db,CancellationToken ct)=>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Payments.Api\PaymentEndpoints.cs:46:        admin.MapPost("/fake-provider/{id:guid}/resolve",async(Guid id,ProviderResolution request,HttpContext http,PaymentsDb db,CancellationToken ct)=>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Payments.Api\PaymentEndpoints.cs:58:        admin.MapPost("/payments/{id:guid}/reconcile",async(Guid id,HttpContext http,PaymentsDb db,CancellationToken ct)=>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Payments.Api\PaymentEndpoints.cs:67:        admin.MapGet("/faults",async(PaymentsDb db,CancellationToken ct)=>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Payments.Api\PaymentEndpoints.cs:69:        admin.MapPut("/faults",(FaultSelection request,HttpContext http,PaymentsDb db,CancellationToken ct)=>SetFaultAsync(app,http,db,request,ct));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Payments.Api\PaymentEndpoints.cs:70:        admin.MapPost("/faults/reset",(HttpContext http,PaymentsDb db,CancellationToken ct)=>SetFaultAsync(app,http,db,new("Success",0),ct));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Reservations.Api\ReservationEndpoints.cs:80:        app.MapGet("/v1/projections/tour-sessions", async (ReservationsDb db, int? page, int? pageSize, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Reservations.Api\ReservationEndpoints.cs:126:        group.MapPost("", async (
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Reservations.Api\ReservationEndpoints.cs:147:        group.MapGet("", (
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Reservations.Api\ReservationEndpoints.cs:164:        group.MapGet("/{id:guid}", (
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Reservations.Api\ReservationEndpoints.cs:171:        group.MapGet("/{id:guid}/timeline", (
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Reservations.Api\ReservationEndpoints.cs:178:        group.MapPost("/{id:guid}/cancel", async (
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Reservations.Api\ReservationEndpoints.cs:215:        admin.MapGet("", async (ReservationsDb db, string? state, int? page, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Reservations.Api\ReservationEndpoints.cs:234:        admin.MapPost("/{id:guid}/retry-compensation", async (
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\LabHosting.cs:142:        app.MapGet("/health/live", () => Results.Ok(new { status = "Alive" })).AllowAnonymous();
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\LabHosting.cs:144:        app.MapGet("/health/ready", async (IOptions<RuntimeDatabaseOptions> options, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\LabHosting.cs:166:        app.MapGet("/v1/status", (HttpContext context) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\LabHosting.cs:173:        app.MapGet("/v1/admin/status", () => new { service, stage = "LAB-007" })
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\Images\ImageEndpoints.cs:24:        api.MapGet("/tours/{tour:guid}/images", (Guid tour, HttpContext ctx, TourImages handler, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\Images\ImageEndpoints.cs:26:        api.MapGet("/admin/tours/{tour:guid}/images", (Guid tour, TourImages handler, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\Images\ImageEndpoints.cs:28:        api.MapPut("/admin/tours/{tour:guid}/images/{id:guid}", async (Guid tour, Guid id, HttpContext ctx, TourImages handler, CancellationToken ct) => {
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\Images\ImageEndpoints.cs:32:        api.MapDelete("/admin/tours/{tour:guid}/images/{id:guid}", async (Guid tour, Guid id, TourImages handler, CancellationToken ct) => {
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Browser\BrowserEndpoints.cs:27:        app.MapGet("/bff/session", async (HttpContext http, IAntiforgery csrf, SignInManager<LabUser> signIn) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Browser\BrowserEndpoints.cs:36:        app.MapPost("/bff/login", async (TokenLogin input, HttpContext http, IAntiforgery csrf,
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Browser\BrowserEndpoints.cs:63:        api.MapPost("/logout", async (HttpContext http, SignInManager<LabUser> signIn, UserManager<LabUser> users) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Browser\BrowserEndpoints.cs:70:        api.MapGet("/tours", (HttpContext h, BrowserForwarder f, CancellationToken ct) => f.SendAsync(h, HttpMethod.Get, "catalog/v1/tours" + h.Request.QueryString, null, ct));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Browser\BrowserEndpoints.cs:71:        api.MapGet("/tours/{id:guid}", (Guid id, HttpContext h, BrowserForwarder f, CancellationToken ct) => f.SendAsync(h, HttpMethod.Get, $"catalog/v1/tours/{id}", null, ct));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Browser\BrowserEndpoints.cs:72:        api.MapGet("/tours/{id:guid}/sessions", (Guid id, HttpContext h, BrowserForwarder f, CancellationToken ct) => f.SendAsync(h, HttpMethod.Get, $"catalog/v1/tours/{id}/sessions", null, ct));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Browser\BrowserEndpoints.cs:73:        api.MapGet("/reservations", (HttpContext h, BrowserForwarder f, CancellationToken ct) => f.SendAsync(h, HttpMethod.Get, "reservations/v1/reservations" + h.Request.QueryString, null, ct));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Browser\BrowserEndpoints.cs:74:        api.MapGet("/reservations/{id:guid}", (Guid id, HttpContext h, BrowserForwarder f, CancellationToken ct) => f.SendAsync(h, HttpMethod.Get, $"reservations/v1/reservations/{id}", null, ct));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Browser\BrowserEndpoints.cs:75:        api.MapGet("/reservations/{id:guid}/timeline", (Guid id, HttpContext h, BrowserForwarder f, CancellationToken ct) => f.SendAsync(h, HttpMethod.Get, $"reservations/v1/reservations/{id}/timeline", null, ct));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Browser\BrowserEndpoints.cs:76:        api.MapPost("/reservations", (CreateReservation body, HttpContext h, BrowserForwarder f, CancellationToken ct) => f.SendAsync(h, HttpMethod.Post, "reservations/v1/reservations", body, ct));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Browser\BrowserEndpoints.cs:77:        api.MapPost("/reservations/{id:guid}/cancel", (Guid id, HttpContext h, BrowserForwarder f, CancellationToken ct) => f.SendAsync(h, HttpMethod.Post, $"reservations/v1/reservations/{id}/cancel", null, ct));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\AccessDenied.razor:1:@page "/access-denied"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\Admin.razor:1:@page "/admin"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\Account.razor:1:@page "/account"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Reservations.Api\Bookings\BookingProjection.cs:216:        api.MapGet("/my-bookings", (ClaimsPrincipal user, int? pageSize, string? cursor, IBookingStore store, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Reservations.Api\Bookings\BookingProjection.cs:219:        api.MapGet("/my-bookings/{id:guid}", async (ClaimsPrincipal user, Guid id, IBookingStore store, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Reservations.Api\Bookings\BookingProjection.cs:225:        api.MapPost("/admin/my-bookings/rebuild", (RebuildBookings request, BookingRebuilder rebuilder, CancellationToken ct) =>
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\Error.razor:1:@page "/Error"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\CatalogAdmin.razor:1:@page "/admin/catalog"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\Login.razor:1:@page "/login"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\NotFound.razor:1:@page "/not-found"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\MyBookings.razor:1:@page "/my-bookings"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\Logout.razor:1:@page "/logout"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\Pending.razor:1:@page "/future-workflows"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\Operations.razor:2:@page "/operations"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\Payments.razor:1:@page "/payments"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\Payments.razor:2:@page "/payments/{Id:guid}"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\Notifications.razor:1:@page "/notifications"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\Notifications.razor:2:@page "/notifications/{Id:guid}"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\ReservationDetail.razor:1:@page "/reservations/{Id:guid}"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\Reservations.razor:1:@page "/reservations"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\Sagas.razor:1:@page "/admin/sagas"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\Simulations.razor:1:@page "/admin/simulations"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\Home.razor:1:@page "/"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Identity\AuthEndpoints.cs:10:        app.MapPost("/auth/login", async (HttpContext context, IAntiforgery antiforgery,
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Identity\AuthEndpoints.cs:29:        app.MapPost("/auth/logout", async (HttpContext context, IAntiforgery antiforgery,
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Identity\AuthEndpoints.cs:40:        app.MapGet("/auth/me", (HttpContext context) => Results.Ok(new
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Identity\AuthEndpoints.cs:48:        app.MapPost("/auth/token", async (TokenLogin input, SignInManager<LabUser> signIn,
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\TourDetail.razor:1:@page "/tours/{Id:guid}"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\TourProjections.razor:2:@page "/tour-projections"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\CatalogEdit.razor:1:@page "/admin/catalog/{Id:guid}"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Components\Pages\Tours.razor:1:@page "/tours"
