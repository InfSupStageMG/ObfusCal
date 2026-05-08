# ADR 0011: Input validation strategy

* **Status:** Accepted
* **Deciders:** Matthias Hendrickx, Gijs Pennings
* **Date:** 2026-05-08

## Context and problem statement

ObfusCal accepts inbound payloads from authenticated users and peer systems. Several endpoints now accept or will soon
accept externally controlled values such as JSON payloads, time windows, iCal feed URLs, and peer base URLs. Without a
consistent validation approach, the project risks inconsistent error handling, SSRF exposure, and abuse through overly
large request windows or batch payloads. Should we introduce FluentValidation, or should we standardize on built-in
DataAnnotations combined with targeted runtime validation?

## Considered options

* FluentValidation
* DataAnnotations + custom runtime validation

## Decision outcome

We chose **DataAnnotations + custom runtime validation**.

Request DTOs use DataAnnotations such as `[Required]` and `[MaxLength]`, while rules that depend on runtime
configuration or infrastructure behavior are enforced in application or infrastructure services. This includes the
maximum query window, the maximum number of pushed shadow slots, and SSRF-safe URL validation through a shared
`IUrlSafetyValidator` abstraction.

## Decision rationale

DataAnnotations fit naturally with ASP.NET Core's built-in `[ApiController]` model validation behavior and make it easy
to return consistent `ValidationProblemDetails` responses without introducing another dependency. At the same time,
some of the rules required by this project are not simple DTO shape checks: SSRF validation requires URL parsing and DNS
resolution, and abuse-resistance limits depend on configurable runtime options. Splitting validation between
DataAnnotations for request shape and custom runtime validation for behavioral constraints keeps the solution simple,
framework-native, and well aligned with the current architecture.

## Consequences

* **Positive:** Request model validation stays lightweight and consistent with the default ASP.NET Core programming model.
* **Positive:** Invalid payloads can be handled uniformly through `ValidationProblemDetails` responses.
* **Positive:** Runtime-dependent security checks such as SSRF blocking and configurable abuse limits remain centralized in reusable services and use cases.
* **Negative:** Some validation logic is split across controller models, application use cases, and infrastructure services rather than being expressed in one fluent rule set.
* **Negative:** More complex cross-field or highly conditional rules are less ergonomic than they would be in FluentValidation.

## Pros and cons of the options

### FluentValidation

* Good, because it provides rich fluent rule composition and is often more expressive for complex and cross-field validation rules.
* Good, because it offers strong test ergonomics when validation rules become large or heavily conditional.
* Bad, because it adds another dependency and framework integration layer to a project that can currently use ASP.NET Core's built-in validation model.
* Bad, because it would be a less natural fit for the existing codebase style, where framework-native conventions are preferred unless the extra complexity clearly pays for itself.

### DataAnnotations + custom runtime validation

* Good, because it requires no additional dependency and integrates directly with `[ApiController]` and `ValidationProblemDetails`.
* Good, because it covers the common request-shape rules in a simple, declarative way.
* Good, because it allows runtime-dependent checks such as SSRF blocking and configurable abuse limits to live in the application and infrastructure layers where they naturally belong.
* Bad, because complex cross-field and conditional validation rules are less ergonomic to express in attributes.
* Bad, because developers must remember that not all validation belongs on DTOs; some rules must still be enforced deeper in the stack.

