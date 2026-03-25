import { Progress } from '@/components/ui/progress';

interface ConfidenceBarProps {
  score: number; // 0.0 to 1.0
  label: string;
}

export function ConfidenceBar({ score, label }: ConfidenceBarProps) {
  const percentage = Math.max(0, Math.min(100, Math.round(score * 100)));
  
  // Determine color based on score
  let indicatorColor = 'bg-red-500';
  if (score >= 0.75) {
    indicatorColor = 'bg-green-500';
  } else if (score >= 0.4) {
    indicatorColor = 'bg-amber-500';
  }

  return (
    <div className="w-full space-y-1.5">
      <div className="flex justify-between text-sm">
        <span className="font-medium text-slate-200">{label}</span>
        <span className="text-slate-400">{percentage}%</span>
      </div>
      <Progress 
        value={percentage} 
        className="h-2 bg-slate-800"
        indicatorClassName={indicatorColor} 
      />
    </div>
  );
}
