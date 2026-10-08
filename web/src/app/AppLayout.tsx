import { Suspense } from "react";
import { Link, NavLink, Outlet } from "react-router-dom";
import { MenuIcon } from "../components/icons";
import { PageSkeleton } from "../components/Skeletons";
import { useLogout, useSignedInUser } from "../features/auth/useAuth";
import { initials } from "../lib/format";

const closeMenus = () => (document.activeElement as HTMLElement | null)?.blur();

export function AppLayout() {
  const user = useSignedInUser();
  const logout = useLogout();
  const links = [
    { to: "/", label: "Worklist", end: true },
    ...(user.role === "Manager"
      ? [
          { to: "/review", label: "Review queue", end: false },
          { to: "/exceptions", label: "Exceptions", end: false },
          { to: "/reconciliation", label: "Reconciliation", end: false },
        ]
      : []),
  ];
  const navItems = links.map((link) => (
    <li key={link.to}>
      <NavLink to={link.to} end={link.end} onClick={closeMenus} className={({ isActive }) => (isActive ? "menu-active" : "")}>
        {link.label}
      </NavLink>
    </li>
  ));

  return (
    <div className="min-h-screen bg-base-200">
      <header className="navbar sticky top-0 z-30 border-b border-base-300 bg-base-100 px-2 sm:px-4">
        <div className="navbar-start gap-1">
          <div className="dropdown lg:hidden">
            <button type="button" tabIndex={0} className="btn btn-ghost btn-square" aria-label="Open navigation">
              <MenuIcon className="size-5" />
            </button>
            <ul tabIndex={0} className="menu dropdown-content z-40 mt-2 w-56 rounded-box bg-base-100 p-2 shadow-lg">{navItems}</ul>
          </div>
          <Link to="/" className="btn btn-ghost gap-2 px-2 text-base font-semibold">
            <span className="grid size-7 place-items-center rounded-lg bg-primary text-xs font-bold text-primary-content">DC</span>
            <span>Denials Command Center</span>
          </Link>
        </div>
        <nav className="navbar-center hidden lg:flex" aria-label="Main">
          <ul className="menu menu-horizontal gap-1">{navItems}</ul>
        </nav>
        <div className="navbar-end">
          <div className="dropdown dropdown-end">
            <button type="button" tabIndex={0} className="btn btn-ghost gap-2 px-2" aria-label="Account menu">
              <span className="avatar avatar-placeholder">
                <span className="w-8 rounded-full bg-neutral text-xs text-neutral-content">{initials(user.displayName)}</span>
              </span>
              <span className="hidden text-sm sm:inline">{user.displayName}</span>
            </button>
            <ul tabIndex={0} className="menu dropdown-content z-40 mt-2 w-56 rounded-box bg-base-100 p-2 shadow-lg">
              <li className="menu-title">
                {user.displayName} · {user.role === "Manager" ? "Practice manager" : "Denials specialist"}
              </li>
              <li>
                <button type="button" onClick={() => logout.mutate()}>Sign out</button>
              </li>
            </ul>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-screen-2xl p-4 sm:p-6">
        <Suspense fallback={<PageSkeleton />}>
          <Outlet />
        </Suspense>
      </main>
    </div>
  );
}
