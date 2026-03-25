import { Progress } from "@/components/ui/progress";

interface ConfidenceBarProps {
  score: number;
  label: string;
}

export function ConfidenceBar({ score, label }: ConfidenceBarProps) {
  const percentage = Math.round(score * 100);
  
  const getBarColor = () => {
    if (score < 0.4) return "bg-red-500";
    if (score < 0.75) return "bg-amber-500";
    return "bg-green-500";
  };

  return (
    <div className="space-y-1.5 w-full">
      <div className="flex justify-between text-xs">
        <span className="text-slate-400 font-medium">{label}</span>
        <span className={`font-mono font-bold ${getBarColor().replace('bg-', 'text-')}`}>
          {percentage}%
        </span>
      </div>
      <Progress 
        value={percentage} 
        className="h-2 bg-slate-850 border border-slate-800/50" 
        indicatorClassName={`${getBarColor()} transition-all duration-1000 ease-out shadow-[0_0_10px_rgba(0,0,0,0.5)]`}
      />
    </div>
  );
}
