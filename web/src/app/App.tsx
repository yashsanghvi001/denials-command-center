import { lazy } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { FullPageLoader } from "../components/Skeletons";
import { LoginPage } from "../features/auth/LoginPage";
import { ResetPasswordPage } from "../features/auth/ResetPasswordPage";
import { useCurrentUser } from "../features/auth/useAuth";
import { AppLayout } from "./AppLayout";

const WorklistPage = lazy(() => import("../features/worklist/WorklistPage").then((module) => ({ default: module.WorklistPage })));
const ClaimPage = lazy(() => import("../features/claims/ClaimPage").then((module) => ({ default: module.ClaimPage })));
const ReviewQueuePage = lazy(() => import("../features/review-queue/ReviewQueuePage").then((module) => ({ default: module.ReviewQueuePage })));
const ExceptionsPage = lazy(() => import("../features/exceptions/ExceptionsPage").then((module) => ({ default: module.ExceptionsPage })));
const ReconciliationPage = lazy(() => import("../features/reconciliation/ReconciliationPage").then((module) => ({ default: module.ReconciliationPage })));

export function App() {
  const { data: user, isPending } = useCurrentUser();
  if (isPending) return <FullPageLoader />;

  if (!user) {
    return (
      <Routes>
        <Route path="reset-password" element={<ResetPasswordPage />} />
        <Route path="*" element={<LoginPage />} />
      </Routes>
    );
  }

  const isManager = user.role === "Manager";
  return (
    <Routes>
      <Route element={<AppLayout />}>
        <Route index element={<WorklistPage />} />
        <Route path="claims/:claimId" element={<ClaimPage />} />
        {isManager && <Route path="review" element={<ReviewQueuePage />} />}
        {isManager && <Route path="exceptions" element={<ExceptionsPage />} />}
        {isManager && <Route path="reconciliation" element={<ReconciliationPage />} />}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
