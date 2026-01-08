import {describe, it, expect} from 'vitest';
import {render, screen} from '@/test/test-utils';
import {MemberAvatar} from './member-avatar';

describe('MemberAvatar', () => {
    it('renders the first letter of the name', () => {
        render(<MemberAvatar name="Alice" color="#3b82f6"/>);
        expect(screen.getByText('A')).toBeInTheDocument();
    });

    it('handles empty name gracefully', () => {
        render(<MemberAvatar name="" color="#3b82f6"/>);
        expect(screen.getByText('?')).toBeInTheDocument();
    });

    it('applies custom className', () => {
        render(<MemberAvatar name="Bob" color="#3b82f6" className="custom-class"/>);
        // The text is inside the div, so getByText returns the div directly
        const avatar = screen.getByText('B');
        expect(avatar).toHaveClass('custom-class');
    });

    it('applies different sizes', () => {
        const {rerender} = render(<MemberAvatar name="Charlie" color="#3b82f6" size="xs"/>);
        expect(screen.getByText('C')).toHaveClass('w-5', 'h-5');

        rerender(<MemberAvatar name="Charlie" color="#3b82f6" size="lg"/>);
        expect(screen.getByText('C')).toHaveClass('w-10', 'h-10');
    });
});
