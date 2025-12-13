from reportlab.lib import colors
from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import cm
from reportlab.platypus import SimpleDocTemplate, Table, TableStyle, Paragraph, Spacer, PageBreak, Image
from reportlab.lib.enums import TA_LEFT, TA_RIGHT, TA_CENTER
from reportlab.graphics.shapes import Drawing, Rect
from reportlab.graphics import renderPDF
from io import BytesIO
from datetime import datetime
import os

def format_currency(value):
    """Format currency value as Euro"""
    if value is None:
        return "€ 0,00"
    return f"€ {value:,.2f}".replace(',', 'X').replace('.', ',').replace('X', '.')

def generate_bank_statement_pdf(summary, transactions):
    """Generate a modern, professional bank statement PDF"""
    
    buffer = BytesIO()
    
    # Create PDF with landscape orientation
    doc = SimpleDocTemplate(
        buffer,
        pagesize=landscape(A4),
        rightMargin=1.5*cm,
        leftMargin=1.5*cm,
        topMargin=1.5*cm,
        bottomMargin=1.5*cm
    )
    
    # Container for PDF elements
    elements = []
    
    # Center Parcs color scheme
    primary_color = colors.HexColor('#6B9E3E')      # Center Parcs green
    primary_dark = colors.HexColor('#4A7C2B')       # Darker green
    success_color = colors.HexColor('#6B9E3E')      # Green for positive amounts
    danger_color = colors.HexColor('#D84315')       # Orange-red for negative
    dark_color = colors.HexColor('#2E3830')         # Dark forest green
    light_bg = colors.HexColor('#F5F7F4')           # Very light green-gray
    border_color = colors.HexColor('#D4DFD0')       # Light green border
    accent_orange = colors.HexColor('#F8991D')      # Center Parcs orange accent
    
    # Styles
    styles = getSampleStyleSheet()
    
    # Title style
    title_style = ParagraphStyle(
        'CustomTitle',
        parent=styles['Heading1'],
        fontSize=28,
        textColor=primary_color,
        spaceAfter=10,
        alignment=TA_CENTER,
        fontName='Helvetica-Bold'
    )
    
    # Subtitle style
    subtitle_style = ParagraphStyle(
        'Subtitle',
        parent=styles['Normal'],
        fontSize=14,
        textColor=dark_color,
        alignment=TA_CENTER,
        spaceAfter=20,
        fontName='Helvetica-Bold'
    )
    
    # Section header style
    section_style = ParagraphStyle(
        'SectionHeader',
        parent=styles['Heading2'],
        fontSize=14,
        textColor=primary_color,
        spaceBefore=15,
        spaceAfter=10,
        fontName='Helvetica-Bold',
        alignment=TA_CENTER
    )
    
    # Cell text style for wrapping
    cell_style = ParagraphStyle(
        'CellText',
        parent=styles['Normal'],
        fontSize=8,
        leading=9,
        wordWrap='CJK'
    )
    
    # Amount style
    amount_style = ParagraphStyle(
        'AmountText',
        parent=styles['Normal'],
        fontSize=8,
        leading=9,
        alignment=TA_RIGHT,
        fontName='Helvetica-Bold'
    )
    # Logo and branding header
    logo_path = os.path.join(os.path.dirname(__file__), 'static', 'images', 'centerparcs-logo.png')
    
    if os.path.exists(logo_path):
        # Add Center Parcs logo
        logo = Image(logo_path, width=6*cm, height=2*cm, kind='proportional')
        logo.hAlign = 'CENTER'
        elements.append(logo)
        elements.append(Spacer(1, 0.3*cm))
    else:
        # Fallback to text branding
        brand_style = ParagraphStyle(
            'Brand',
            parent=styles['Normal'],
            fontSize=32,
            textColor=primary_color,
            alignment=TA_CENTER,
            fontName='Helvetica-Bold',
            spaceAfter=5
        )
        brand = Paragraph("Center Parcs", brand_style)
        elements.append(brand)
        elements.append(Spacer(1, 0.5*cm))
    
    # Title
    title = Paragraph("Bank Statement", title_style)
    elements.append(title)
    
    # Subtitle with account name
    subtitle = Paragraph(f"{summary.get('account_name', 'N/A')}", subtitle_style)
    elements.append(subtitle)
    elements.append(Spacer(1, 0.3*cm))
    
    # Account Info and Period Info in modern card-style layout
    account_data = [
        ['IBAN', summary.get('iban', 'N/A')],
        ['Account Holder', summary.get('account_name', 'N/A')]
    ]
    
    period_data = [
        ['Statement Period', f"{summary.get('date_from', 'N/A')} - {summary.get('date_to', 'N/A')}"],
        ['Generated', datetime.now().strftime('%d-%m-%Y %H:%M')]
    ]
    
    account_table = Table(account_data, colWidths=[4*cm, 8*cm])
    account_table.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, -1), light_bg),
        ('FONTNAME', (0, 0), (0, -1), 'Helvetica-Bold'),
        ('FONTSIZE', (0, 0), (-1, -1), 9),
        ('TEXTCOLOR', (0, 0), (0, -1), primary_color),
        ('ALIGN', (0, 0), (0, -1), 'LEFT'),
        ('ALIGN', (1, 0), (1, -1), 'LEFT'),
        ('VALIGN', (0, 0), (-1, -1), 'MIDDLE'),
        ('LEFTPADDING', (0, 0), (-1, -1), 12),
        ('RIGHTPADDING', (0, 0), (-1, -1), 12),
        ('TOPPADDING', (0, 0), (-1, -1), 10),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 10),
        ('ROUNDEDCORNERS', [6, 6, 6, 6]),
        ('LINEBELOW', (0, 0), (-1, 0), 2, primary_color),
    ]))
    
    period_table = Table(period_data, colWidths=[4*cm, 8*cm])
    period_table.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, -1), light_bg),
        ('FONTNAME', (0, 0), (0, -1), 'Helvetica-Bold'),
        ('FONTSIZE', (0, 0), (-1, -1), 9),
        ('TEXTCOLOR', (0, 0), (0, -1), primary_color),
        ('ALIGN', (0, 0), (0, -1), 'LEFT'),
        ('ALIGN', (1, 0), (1, -1), 'LEFT'),
        ('VALIGN', (0, 0), (-1, -1), 'MIDDLE'),
        ('LEFTPADDING', (0, 0), (-1, -1), 12),
        ('RIGHTPADDING', (0, 0), (-1, -1), 12),
        ('TOPPADDING', (0, 0), (-1, -1), 10),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 10),
        ('ROUNDEDCORNERS', [6, 6, 6, 6]),
        ('LINEBELOW', (0, 0), (-1, 0), 2, primary_color),
    ]))
    
    info_table = Table([[account_table, period_table]], colWidths=[12*cm, 12*cm])
    info_table.setStyle(TableStyle([
        ('LEFTPADDING', (0, 0), (-1, -1), 0),
        ('RIGHTPADDING', (0, 0), (-1, -1), 0),
    ]))
    elements.append(info_table)
    elements.append(Spacer(1, 0.7*cm))
    
    # Section header for balance summary
    elements.append(Paragraph("Balance Summary", section_style))
    
    # Balance Summary with modern styling and centered headers
    balance_data = [[
        Paragraph('<para align="center"><b><font color="white">Opening Balance</font></b></para>', cell_style),
        Paragraph('<para align="center"><b><font color="white">Closing Balance</font></b></para>', cell_style),
        Paragraph('<para align="center"><b><font color="white">Total Debited</font></b></para>', cell_style),
        Paragraph('<para align="center"><b><font color="white">Total Credited</font></b></para>', cell_style),
        Paragraph('<para align="center"><b><font color="white">Transactions</font></b></para>', cell_style)
    ], [
        format_currency(summary.get('opening_balance', 0)),
        format_currency(summary.get('closing_balance', 0)),
        format_currency(summary.get('total_debited', 0)),
        format_currency(summary.get('total_credited', 0)),
        str(summary.get('transaction_count', 0))
    ]]
    
    balance_table = Table(balance_data, colWidths=[4.8*cm, 4.8*cm, 4.8*cm, 4.8*cm, 4.8*cm])
    balance_table.setStyle(TableStyle([
        # Header
        ('BACKGROUND', (0, 0), (-1, 0), primary_color),
        ('TEXTCOLOR', (0, 0), (-1, 0), colors.white),
        ('FONTNAME', (0, 0), (-1, 0), 'Helvetica-Bold'),
        ('FONTSIZE', (0, 0), (-1, 0), 10),
        ('ALIGN', (0, 0), (-1, 0), 'CENTER'),
        
        # Values
        ('BACKGROUND', (0, 1), (-1, 1), colors.white),
        ('FONTNAME', (0, 1), (-1, 1), 'Helvetica-Bold'),
        ('FONTSIZE', (0, 1), (-1, 1), 12),
        ('ALIGN', (0, 1), (-1, 1), 'CENTER'),
        ('VALIGN', (0, 0), (-1, -1), 'MIDDLE'),
        ('ROUNDEDCORNERS', [8, 8, 8, 8]),
        ('INNERGRID', (0, 0), (-1, -1), 0.5, border_color),
        ('TOPPADDING', (0, 0), (-1, -1), 14),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 14),
    ]))
    elements.append(balance_table)
    elements.append(Spacer(1, 0.7*cm))
    
    # Page break before transactions
    elements.append(PageBreak())
    
    # Section header for transactions
    elements.append(Paragraph("Transaction Details", section_style))
    
    # Transactions Table with centered headers
    trans_data = [[
        Paragraph('<para align="center"><b><font color="white">Value Date</font></b></para>', cell_style),
        Paragraph('<para align="center"><b><font color="white">Type</font></b></para>', cell_style),
        Paragraph('<para align="center"><b><font color="white">Counterparty</font></b></para>', cell_style),
        Paragraph('<para align="center"><b><font color="white">Description</font></b></para>', cell_style),
        Paragraph('<para align="center"><b><font color="white">Process Date</font></b></para>', cell_style),
        Paragraph('<para align="center"><b><font color="white">Amount</font></b></para>', cell_style)
    ]]
    
    for tx in transactions:
        # Determine counterparty based on amount (negative = creditor, positive = debtor)
        counterparty = ''
        if tx.get('transaction_amount', 0) < 0:
            # Negative = outgoing = show creditor
            if tx.get('creditor_iban'):
                counterparty_name = tx.get('creditor_name', '')
                counterparty = f"{tx.get('creditor_iban')}<br/>{counterparty_name}"
        else:
            # Positive = incoming = show debtor
            if tx.get('debtor_iban'):
                counterparty_name = tx.get('debtor_name', '')
                counterparty = f"{tx.get('debtor_iban')}<br/>{counterparty_name}"
        
        # Value date
        value_date = tx.get('valuedate')  # Changed from 'value_date' to match DB query
        if hasattr(value_date, 'strftime'):
            value_date = value_date.strftime('%d-%m-%Y')
        else:
            value_date = str(value_date) if value_date else '-'
            
        process_date = tx.get('processdate')
        if hasattr(process_date, 'strftime'):
            process_date = process_date.strftime('%d-%m-%Y')
        else:
            process_date = str(process_date) if process_date else '-'
        
        # Type - prefer type name, fallback to detailed type
        tx_type = ''
        if tx.get('rabo_transaction_type_name'):
            tx_type = str(tx.get('rabo_transaction_type_name'))
        else:
            tx_type = str(tx.get('rabo_detailed_transaction_type', ''))
        
        # Description - use Paragraph for wrapping
        description = tx.get('description', '') or ''
        
        # Format amount with color (Center Parcs orange-red for negative, green for positive)
        amount = tx.get('transaction_amount', 0)
        amount_text = format_currency(amount)
        if amount < 0:
            amount_text = f'<font color="#D84315">{amount_text}</font>'
        else:
            amount_text = f'<font color="#6B9E3E">{amount_text}</font>'
        
        trans_data.append([
            value_date,
            tx_type,
            Paragraph(counterparty or '-', cell_style),
            Paragraph(description or '-', cell_style),
            process_date,
            Paragraph(amount_text, amount_style)
        ])
    
    trans_table = Table(trans_data, colWidths=[2.8*cm, 2*cm, 5.5*cm, 7*cm, 2.8*cm, 3*cm], repeatRows=1)
    trans_table.setStyle(TableStyle([
        # Header
        ('BACKGROUND', (0, 0), (-1, 0), primary_color),
        ('TEXTCOLOR', (0, 0), (-1, 0), colors.white),
        ('FONTNAME', (0, 0), (-1, 0), 'Helvetica-Bold'),
        ('FONTSIZE', (0, 0), (-1, 0), 9),
        
        # Values
        ('BACKGROUND', (0, 1), (-1, -1), colors.white),
        ('FONTSIZE', (0, 1), (-1, -1), 8),
        ('ALIGN', (0, 1), (0, -1), 'CENTER'),  # Value date
        ('ALIGN', (1, 1), (1, -1), 'CENTER'),  # Type
        ('ALIGN', (2, 1), (3, -1), 'LEFT'),    # Counterparty, Description
        ('ALIGN', (4, 1), (4, -1), 'CENTER'),  # Process date
        ('ALIGN', (5, 1), (5, -1), 'RIGHT'),   # Amount
        
        ('VALIGN', (0, 0), (-1, -1), 'TOP'),
        ('ROUNDEDCORNERS', [8, 8, 8, 8]),
        ('INNERGRID', (0, 0), (-1, -1), 0.3, border_color),
        ('LEFTPADDING', (0, 0), (-1, -1), 10),
        ('RIGHTPADDING', (0, 0), (-1, -1), 10),
        ('TOPPADDING', (0, 0), (-1, -1), 8),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 8),
    ]))
    elements.append(trans_table)
    
    # Footer
    elements.append(Spacer(1, 0.8*cm))
    footer_style = ParagraphStyle(
        'Footer',
        parent=styles['Normal'],
        fontSize=8,
        textColor=colors.HexColor('#6B7F68'),
        alignment=TA_CENTER
    )
    footer = Paragraph(f"Generated on {datetime.now().strftime('%d-%m-%Y at %H:%M')} | Center Parcs De Eemhof Financial Services", footer_style)
    elements.append(footer)
    
    # Build PDF
    doc.build(elements)
    
    buffer.seek(0)
    return buffer
