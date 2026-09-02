function onLoad(executionContext) {
    toggleShippingAddressVisibility(executionContext);
    registerHandlers(executionContext);
}

// Function to register event handlers
function registerHandlers(executionContext) {
    try {
        const formContext = executionContext.getFormContext();

        // Register event handler for the checkbox
        formContext.getAttribute("soap_sameasbillingaddress").addOnChange(function () {
            toggleShippingAddressVisibility(executionContext);
            copyBillingToShippingAddress(executionContext);
        });

        // Register event handlers for billing address fields to copy when they change
        const billingFields = [
            "address1_line1",
            "address1_line2",
            "address1_line3",
            "address1_city",
            "address1_stateorprovince",
            "address1_postalcode",
            "address1_country"
        ];

        billingFields.forEach(field => {
            const attribute = formContext.getAttribute(field);
            if (attribute) {
                attribute.addOnChange(function () {
                    copyBillingToShippingAddress(executionContext);
                });
            }
        });

        const telephoneAttribute = formContext.getAttribute("telephone1");
        if (telephoneAttribute) {
            telephoneAttribute.addOnChange(function () {
                formatPhoneNumber(executionContext);
            });
        }

        formContext.data.entity.addOnSave(formatPhoneNumber);

    } catch (error) {
        console.error("Error in handling: ", error.message);
    }
}


// Show/hide address2 fields based on soap_sameasbillingaddress
function toggleShippingAddressVisibility(executionContext) {
    try {
        const formContext = executionContext.getFormContext();
        const isSameAsShipping = formContext.getAttribute("soap_sameasbillingaddress").getValue();

        // List of shipping address fields to hide/show
        const shippingAddressFields = [
            "address2_line1",
            "address2_line2",
            "address2_line3",
            "address2_city",
            "address2_stateorprovince",
            "address2_postalcode",
            "address2_country"
        ];

        // Hide or show shipping address fields based on checkbox value
        shippingAddressFields.forEach(field => {
            const control = formContext.getControl(field);
            if (control) {
                control.setVisible(!isSameAsShipping);
            }
        });

    } catch (error) {
        console.error("Error in toggleShippingAddressVisibility: ", error.message);
    }
}

// Copy address from address1 to address2 when needed
function copyBillingToShippingAddress(executionContext) {
    try {
        const formContext = executionContext.getFormContext();
        const isSameAsShipping = formContext.getAttribute("soap_sameasbillingaddress").getValue();

        // Only proceed if "Same as billing address" is checked
        if (!isSameAsShipping) {
            return;
        }

        // Map of billing address fields to corresponding shipping address fields
        const addressFieldsMap = {
            "address1_line1": "address2_line1",
            "address1_line2": "address2_line2",
            "address1_line3": "address2_line3",
            "address1_city": "address2_city",
            "address1_stateorprovince": "address2_stateorprovince",
            "address1_postalcode": "address2_postalcode",
            "address1_country": "address2_country"
        };

        // Copy each field value from address1 to address2
        for (const [billingField, shippingField] of Object.entries(addressFieldsMap)) {
            const billingAttribute = formContext.getAttribute(billingField);
            const shippingAttribute = formContext.getAttribute(shippingField);

            if (billingAttribute && shippingAttribute) {
                const billingValue = billingAttribute.getValue();
                shippingAttribute.setValue(billingValue);
            }
        }

    } catch (error) {
        console.error("Error in copyBillingToShippingAddress: ", error.message);
    }
}


/**
 * Formats the telephone1 field on the form using normalizePhoneNumber
 * @param {object} executionContext - The execution context from the form event
 */
function formatPhoneNumber(executionContext) {
    try {
        const formContext = executionContext.getFormContext();
        const telephoneAttribute = formContext.getAttribute("telephone1");
        const telephoneControl = formContext.getControl("telephone1");
        const currentValue = telephoneAttribute.getValue();

        // If there's no value, clear notifications and return
        if (!currentValue) {
            telephoneControl.clearNotification("invalidPhoneError");
            return;
        }

        const normalizedValue = normalizePhoneNumber(currentValue);

        // Only update if the value is valid and different
        if (normalizedValue !== null && normalizedValue !== currentValue) {
            telephoneAttribute.setValue(normalizedValue);
            telephoneControl.clearNotification("invalidPhoneError");
        } else if (normalizedValue === null) {

            // Set an error on the control to prevent save
            telephoneControl.setNotification(
                "Invalid phone format. Required format: nnn-nnn-nnnn",
                "invalidPhoneError"
            );

            // If this is a save event, prevent it
            if (executionContext.getEventArgs &&
                executionContext.getEventArgs().getEventSource &&
                executionContext.getEventArgs().getEventSource() === "save") {
                executionContext.getEventArgs().preventDefault();
            }
        }

    } catch (error) {
        console.error("Error in formatPhoneNumber: ", error.message);
    }
}

/**
* Normalizes a phone number to the format nnn-nnn-nnnn
* Accepts formats: nnn-nnn-nnnn, nnnnnnnnnn, +1nnnnnnnnnn
* @param {string} phone - The phone number to normalize
* @returns {string|null} - Normalized phone number or null if invalid
*/
function normalizePhoneNumber(phone) {
    // Return null if input is null or undefined
    if (!phone) {
        return null;
    }

    // Remove all non-digit characters
    const digits = phone.replace(/\D/g, '');

    // If the number starts with 1 (country code), remove it
    const cleanDigits = digits.startsWith('1') ? digits.substring(1) : digits;

    // Check if we have exactly 10 digits after cleaning
    if (cleanDigits.length !== 10) {
        return null;
    }

    // Format as nnn-nnn-nnnn
    return `${cleanDigits.substring(0, 3)}-${cleanDigits.substring(3, 6)}-${cleanDigits.substring(6, 10)}`;
}