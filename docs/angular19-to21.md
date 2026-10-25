# Angular 19 to 21: comparison through this application

This repository runs Angular21 only. The comparison is preparation, not a second supported build or
an assertion about an employer's tooling. Official sources checked2026-10-24; consult them again
before upgrading. The project intentionally retains Angular21 even when newer majors are available.

| Topic | Angular19 context | Angular21 implementation here |
|---|---|---|
| Node/TypeScript | 19.2 supports Node18.19.1/20.11.1/22 ranges and TypeScript>=5.5,<5.9 | 21.2 supports Node20.19/22.12/24 ranges and TypeScript>=5.9,<6; this lock pins Node24.18,TS5.9.3 |
| RxJS | Compatible RxJS6.5.3 or7.4 ranges | Keep RxJS7.8.2, typed HTTP, cancellation and observable action completion |
| Components | Standalone is already the default in19; existing NgModules remain relevant | Standalone components, explicit template imports and provideRouter/provideHttpClient in main.ts |
| Shared state | Actions/selectors are distinct from a component's transient form values | NGXS21.0 with feature states; selectors consumed via selectSignal, typed action handlers return observables |
| Reactivity | Signals and RxJS coexist; neither implies global state persistence | Signal reads render state, RxJS handles transport/cancellation, NGXS coordinates shared state and session resets |
| Tests | Older workspaces commonly retain Karma/Jasmine or custom runners; inspect the actual builder | Angular build unit-test builder with Vitest4.1.12/jsdom; HttpTestingController and actual Store for deterministic state tests |
| Migration | Never just edit the major package number | Check compatibility/peer dependencies, run official migrations one major at a time in an isolated branch, inspect generated changes and rerun unit/browser tests |

Compatibility ranges come from [Angular's official version table](https://angular.dev/reference/versions).
Standalone behavior is documented in the [Angular19 component guide](https://v19.angular.dev/guide/components).
The [Angular roadmap](https://angular.dev/roadmap) describes stable Vitest integration in21.
These are version-specific observations, not a claim that every Angular19 application must change its
runner, use signals everywhere or remove NgModules. Zone/change-detection strategy is a separate
migration decision; do not infer it solely from standalone components.

Trace a concrete example: BookingForm dispatches SubmitBooking; ReservationsState returns the HTTP
observable and preserves the idempotency key for an unchanged retry; a typed API performs transport.
The page reads ReservationsState.view through selectSignal. Local participant form validation stays
in the component; server validation remains authoritative. See [NGXS selection](https://www.ngxs.io/concepts/select)
and [Angular signals](https://angular.dev/guide/signals) for their distinct responsibilities.

Read auth/session-boundary.spec.ts next. A real interceptor401 advances the session generation and
resets every feature. A late receipt response from the previous user cannot restore private state.
This is a data isolation requirement, not just an Angular rendering optimization. Browser guards
improve navigation; cookie/CSRF/backend JWT/role/ownership checks enforce security independently.
No NGXS/localStorage persistence plugin or browser JWT is introduced.

Practice: change a tour filter while its first HTTP response is pending; explain why cancelUncompleted
and generation/request guards are needed at different boundaries. Then explain how firstValueFrom
at initialization differs from subscribing to a long-lived stream. Use the real tests as evidence;
compiling a component or passing jsdom is not proof of secure Chromium navigation.
