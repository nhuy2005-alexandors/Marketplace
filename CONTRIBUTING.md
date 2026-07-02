# Contributing to E-Commerce

First off, thank you for considering contributing to this project! It's people like you that make this tool great.

## Getting Started

1. **Clone the repository** locally.
2. **Setup Backend**:
   - Navigate to the `src` directory.
   - Run `dotnet restore` to install dependencies.
   - Setup the database connections if necessary.
   - Run `dotnet run --project ECommerce.API` to start the backend.
3. **Setup Frontend**:
   - Navigate to the `client` directory.
   - Run `npm install` to install dependencies.
   - Run `npm run dev` to start the development server.

## Branching Strategy

- `master` is our main production branch.
- For new features or bugs, create a new branch from `master` using the convention:
  - `feature/<feature-name>` for features
  - `bugfix/<bug-name>` for bug fixes
  - `hotfix/<hotfix-name>` for urgent fixes

## Commit Messages

We use standard commit messages:
- `feat:` for new features
- `fix:` for bug fixes
- `docs:` for documentation changes
- `style:` for formatting, missing semi colons, etc
- `refactor:` for refactoring code
- `test:` for adding missing tests
- `chore:` for updating build tasks, package manager configs, etc

## Pull Requests

1. Ensure your code follows the `.editorconfig` standards.
2. Ensure you have tested your code locally.
3. Create a Pull Request against the `master` branch.
4. Fill out the Pull Request template completely.
5. Request a review from at least one other team member.
