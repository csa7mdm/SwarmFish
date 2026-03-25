import { render, screen } from '@testing-library/react';
import { ConfidenceBar } from '@/components/ConfidenceBar';

describe('ConfidenceBar', () => {
  it('renders label and percentage correctly', async () => {
    render(<ConfidenceBar score={0.82} label="Test Confidence" />);
    
    expect(await screen.findByText('Test Confidence')).toBeInTheDocument();
    expect(await screen.findByText('82%')).toBeInTheDocument();
  });

  it('handles boundaries (0 and 1)', async () => {
    const { unmount } = render(<ConfidenceBar score={0} label="Min" />);
    expect(await screen.findByText('0%')).toBeInTheDocument();
    unmount();
    
    render(<ConfidenceBar score={1} label="Max" />);
    expect(await screen.findByText('100%')).toBeInTheDocument();
  });
  
  // Note: We can't easily test the indicatorClasses applied via props 
  // since shadcn's internal Progress component might not expose it directly
  // to testing-library in this DOM environment setup, 
  // but we test that it generally functions.
});
