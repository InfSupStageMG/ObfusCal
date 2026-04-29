# ADR 0008: Project code architecture/structure: Clean Architecture

* **Status:** Accepted
* **Deciders:** Matthias Hendrickx, Gijs Pennings, Coach
* **Date:** 2026-04-23

## Context and problem statement

The project is expected to grow in complexity as we add features and support more calendar providers. We need a clear architectural structure to manage this complexity, facilitate collaboration, and ensure maintainability. How should we structure the codebase to achieve these goals?

## Considered options

* Clean Architecture
* Vertical Slicing
* Modular Monolith
* Vertical Slicing with Clean Architecture per slice

## Decision outcome

We chose **Clean Architecture**.

## Decision rationale

We started out with a simple layered architecture, but found this to be too messy as the project grew. We then considered switching to vertical slicing, but after consideration decided against it, seeing as our application will not focus so much on a high number of features, but rather on a few complex features. We also considered a modular monolith, but this felt like overkill for the project. Clean Architecture provides a clear separation of concerns and allows us to manage complexity effectively as the project grows.

## Pros and cons of the options

### Clean Architecture

* Good, because the strict dependency rule keeps business logic completely isolated from frameworks and infrastructure, making the core domain easy to reason about and test.
* Good, because it scales well with complex domains — as features grow in depth rather than breadth, the layered separation prevents business rules from leaking into the wrong place.
* Good, because every layer has a single, well-understood responsibility, making it straightforward for any developer to know where new code belongs.
* Bad, because it introduces more boilerplate than simpler approaches, particularly around mapping between layers and defining repository interfaces before implementing them.

### Vertical Slicing

* Good, because each feature is fully self-contained in one folder, making it easy to add or modify a feature without touching unrelated code.
* Good, because new contributors can understand and work on a single feature without needing to understand the entire codebase first.
* Bad, because it relies heavily on team discipline to prevent duplication — similar patterns tend to get copy-pasted across features without a shared enforcing structure.
* Bad, because it is a poor fit for applications with a small number of complex features, where the overhead of slicing adds structure without meaningful benefit.

### Modular Monolith

* Good, because each module is independently developable with clearly enforced boundaries, making large teams or parallel workstreams viable without stepping on each other.
* Good, because bounded contexts are explicit and well-enforced, making it easier to extract a module into a separate service later if needed.
* Bad, because it carries the highest upfront architectural complexity of all considered options, requiring significant discipline to set up and maintain correctly.
* Bad, because the overhead of defining module boundaries, contracts, and inter-module communication is disproportionate for a project of this size and scope.

### Vertical Slicing with Clean Architecture per slice

* Good, because it combines the contributor-friendliness of vertical slicing with the domain isolation guarantees of Clean Architecture within each feature.
* Good, because pull requests are naturally scoped to a single feature folder, making code review clean and focused.
* Bad, because it introduces the most structural complexity of all options — developers must understand both vertical slicing conventions and Clean Architecture rules simultaneously.
* Bad, because shared domain concepts that span multiple features (e.g., a `Customer` referenced by both Orders and Invoices) are awkward to place and risk being duplicated across slices.
