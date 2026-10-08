import { appConfig } from "../../lib/config";

export const passwordRules = [
  { label: `At least ${appConfig.passwordMinLength} characters`, test: (password: string) => password.length >= appConfig.passwordMinLength },
  { label: "At least one letter", test: (password: string) => /\p{L}/u.test(password) },
  { label: "At least one number", test: (password: string) => /\d/.test(password) },
];

export const meetsPasswordRules = (password: string) => passwordRules.every((rule) => rule.test(password));
