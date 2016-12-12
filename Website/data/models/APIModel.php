<?php

class APIModel extends Model {
    
    function __construct() {
        parent::__construct();
    }
    
    public function GetUserByID($idArr) {
        $result = array();
        
        foreach ($idArr as $id) {
            $row = $this->db->Query(
                'SELECT `id`, `email`, `username`, `account_type`, `created`, `editted` FROM users WHERE `id` = :id',
                array(
                    "id" => $id
                )
            );
            
            if ($row != array()) { array_push($result, $row); }
        }
        
        if ($result != array() && $result != array(array())) {
            if (count($result) == 1) { return $result[0]; }
            else { return $result; }
        } else {
            return false;
        }
    }
    
    public function GetUserByUsername($usernameArr) {
        $result = array();
        
        foreach ($usernameArr as $username) {
            $row = $this->db->Query(
                'SELECT `id`, `email`, `username`, `account_type`, `created`, `editted` FROM users WHERE `username` = :username',
                array(
                    "username" => $username
                )
            );
            
            if ($row != array()) { array_push($result, $row); }
        }
        
        if ($result != array() && $result != array(array())) {
            if (count($result) == 1) { return $result[0]; }
            else { return $result; }
        } else {
            return false;
        }
    }
    
    public function GetUserByEmail($emailArr) {
        $result = array();
        
        foreach ($emailArr as $email) {
            $row = $this->db->Query(
                'SELECT `id`, `email`, `username`, `account_type`, `created`, `editted` FROM users WHERE `email` = :email',
                array(
                    "email" => $email
                )
            );
            
            if ($row != array()) { array_push($result, $row); }
        }
        
        if ($result != array() && $result != array(array())) {
            if (count($result) == 1) { return $result[0]; }
            else { return $result; }
        } else {
            return false;
        }
    }
    
    public function GetLastInsertedUser() {
        return $this->db->Query(
            "SELECT (`id`) FROM `users` ORDER BY `id` DESC LIMIT 1"
        );
    }
}