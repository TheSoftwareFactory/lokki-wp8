"""
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
"""
"""
Common methods to be used in converting localization files.

Copyright: F-Secure, 2012
"""
#defult product name in localization files
PRODUCT_NAME_DEFAULT_VALUE =  "F-Secure Mobile Sync"
# id that defines default product name in localization file
PRODUCT_NAME_ID = "PRODUCT_NAME_LONG"

def find_node(nodes, string_id):
    """ 
    Searches nodes and finds the node which contains attribute 'string_id'
    Raises exception if suitable node is not found
    """

    for node in nodes:
        current_id = node.getAttribute("id")
        if current_id == string_id:
		    return node
    raise Exception("find_node failed! " + string_id + " was not found from nodes." )

class LocCustomizer():
    
    def __init__(self):
        self.productName = ""

    def convert_product_name(self, string_value, string_id):
        """
        Replaces product name from string_value if it exists
        NOTE that when this method is called first time it should be called by using
        PRODUCT_NAME_ID as a string_id value, so that customized product name is set correctly
        """
        #Set correct product name
        if string_id == PRODUCT_NAME_ID:
            #Remove quotes for the begin and end of the string if they exists
            if string_value[0] == "\"" and string_value[len(string_value)-1] == "\"":
                self.productName = string_value[1:-1]
            else:
                self.productName = string_value
        else:
            if self.productName == "":
                raise Exception("Product name is not set. It should be first item in localization xml")
            if self.productName != PRODUCT_NAME_DEFAULT_VALUE:
                #Default product name has been changed. Change that also from this string if it exists
                string_value = string_value.replace(PRODUCT_NAME_DEFAULT_VALUE, self.productName)
        return string_value
