// The suite shares one Postgres and resets it with Respawn when each test class boots its API.
// Running tests one at a time keeps that reset from ever landing in the middle of another test.
[assembly: NotInParallel]
