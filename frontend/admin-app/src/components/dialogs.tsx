import { CheckCircle2, Info, XCircle } from "lucide-react";
import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { Button, Dialog, Input, Label } from "@/components/ui";
import { cn } from "@/lib/cn";

// Imperative, promise-based replacements for window.confirm / window.prompt / window.alert.
// Confirm + prompt are proper modals; transient messages surface as toasts.

export interface ConfirmOptions {
  title: string;
  message?: ReactNode;
  confirmText?: string;
  cancelText?: string;
  tone?: "primary" | "danger";
}

export interface PromptOptions {
  title: string;
  message?: ReactNode;
  label?: string;
  defaultValue?: string;
  placeholder?: string;
  confirmText?: string;
  required?: boolean;
}

type ToastTone = "error" | "success" | "info";
interface Toast {
  id: number;
  message: string;
  tone: ToastTone;
}

interface DialogApi {
  confirm: (opts: ConfirmOptions) => Promise<boolean>;
  prompt: (opts: PromptOptions) => Promise<string | null>;
  toast: (t: string | { message: string; tone?: ToastTone }) => void;
}

type Active =
  | { kind: "confirm"; opts: ConfirmOptions; resolve: (v: boolean) => void }
  | { kind: "prompt"; opts: PromptOptions; resolve: (v: string | null) => void }
  | null;

const DialogContext = createContext<DialogApi | null>(null);

export function useDialogs(): DialogApi {
  const ctx = useContext(DialogContext);
  if (!ctx) {
    throw new Error("useDialogs must be used within a DialogProvider");
  }
  return ctx;
}

export function DialogProvider({ children }: { children: ReactNode }) {
  const [active, setActive] = useState<Active>(null);
  const [value, setValue] = useState("");
  const [toasts, setToasts] = useState<Toast[]>([]);
  const nextId = useRef(0);

  const confirm = useCallback(
    (opts: ConfirmOptions) =>
      new Promise<boolean>((resolve) => setActive({ kind: "confirm", opts, resolve })),
    [],
  );

  const prompt = useCallback(
    (opts: PromptOptions) =>
      new Promise<string | null>((resolve) => {
        setValue(opts.defaultValue ?? "");
        setActive({ kind: "prompt", opts, resolve });
      }),
    [],
  );

  const toast = useCallback((t: string | { message: string; tone?: ToastTone }) => {
    const id = nextId.current++;
    const item: Toast =
      typeof t === "string" ? { id, message: t, tone: "info" } : { id, message: t.message, tone: t.tone ?? "info" };
    setToasts((prev) => [...prev, item]);
    setTimeout(() => setToasts((prev) => prev.filter((x) => x.id !== id)), 4500);
  }, []);

  const api = useMemo<DialogApi>(() => ({ confirm, prompt, toast }), [confirm, prompt, toast]);

  const finishConfirm = (result: boolean) => {
    if (active?.kind === "confirm") active.resolve(result);
    setActive(null);
  };
  const finishPrompt = (result: string | null) => {
    if (active?.kind === "prompt") active.resolve(result);
    setActive(null);
  };

  const promptValid = active?.kind === "prompt" ? !active.opts.required || value.trim().length > 0 : true;

  return (
    <DialogContext.Provider value={api}>
      {children}

      {active?.kind === "confirm" && (
        <Dialog
          open
          onClose={() => finishConfirm(false)}
          title={active.opts.title}
          footer={
            <>
              <Button variant="secondary" onClick={() => finishConfirm(false)}>
                {active.opts.cancelText ?? "Cancel"}
              </Button>
              <Button variant={active.opts.tone === "danger" ? "danger" : "primary"} onClick={() => finishConfirm(true)} autoFocus>
                {active.opts.confirmText ?? "Confirm"}
              </Button>
            </>
          }
        >
          {active.opts.message ? (
            <p className="text-sm text-[var(--muted)]">{active.opts.message}</p>
          ) : null}
        </Dialog>
      )}

      {active?.kind === "prompt" && (
        <Dialog
          open
          onClose={() => finishPrompt(null)}
          title={active.opts.title}
          footer={
            <>
              <Button variant="secondary" onClick={() => finishPrompt(null)}>
                Cancel
              </Button>
              <Button disabled={!promptValid} onClick={() => finishPrompt(value.trim())}>
                {active.opts.confirmText ?? "OK"}
              </Button>
            </>
          }
        >
          {active.opts.message ? <p className="mb-3 text-sm text-[var(--muted)]">{active.opts.message}</p> : null}
          {active.opts.label ? <Label>{active.opts.label}</Label> : null}
          <Input
            autoFocus
            value={value}
            placeholder={active.opts.placeholder}
            onChange={(e) => setValue(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter" && promptValid) finishPrompt(value.trim());
            }}
          />
        </Dialog>
      )}

      <ToastStack toasts={toasts} />
    </DialogContext.Provider>
  );
}

function ToastStack({ toasts }: { toasts: Toast[] }) {
  if (toasts.length === 0) return null;
  return (
    <div className="fixed bottom-4 right-4 z-[60] flex w-80 max-w-[calc(100vw-2rem)] flex-col gap-2">
      {toasts.map((t) => (
        <div
          key={t.id}
          role="status"
          className={cn(
            "flex items-start gap-2 rounded-lg border px-3 py-2.5 text-sm shadow-lg",
            "bg-[var(--surface)] text-[var(--fg)]",
            t.tone === "error" && "border-[var(--danger)]/30",
            t.tone === "success" && "border-[var(--teal)]/30",
            t.tone === "info" && "border-[var(--border)]",
          )}
        >
          <span className="mt-0.5 shrink-0">
            {t.tone === "error" ? (
              <XCircle className="h-4 w-4 text-[var(--danger)]" />
            ) : t.tone === "success" ? (
              <CheckCircle2 className="h-4 w-4 text-[var(--teal)]" />
            ) : (
              <Info className="h-4 w-4 text-[var(--muted)]" />
            )}
          </span>
          <span>{t.message}</span>
        </div>
      ))}
    </div>
  );
}
